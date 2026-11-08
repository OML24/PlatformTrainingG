using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Rules;

namespace ZuLms.Infrastructure.Persistence.Seed;

public sealed class DbSeeder(
    AppDbContext db, IClock clock, IPasswordHasher passwordHasher)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SeedRolesAsync(cancellationToken);
        await SeedPermissionsAsync(cancellationToken);
        await SeedSettingsAsync(cancellationToken);
        await SeedHintsAsync(cancellationToken);
        await SeedPasswordPolicyAsync(cancellationToken);
        await SeedBlockedPasswordsAsync(cancellationToken);
        await SeedOrganizationAsync(cancellationToken);
        await SeedTracksAsync(cancellationToken);
        await SeedEvaluationFormAsync(cancellationToken);
        await SeedProfessionalCategoriesAsync(cancellationToken);
        await SeedBootstrapAdminAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        var roles = new (RoleCode Code, string Ar, string En, bool Assignable)[]
        {
            (RoleCode.Admin, "مسؤول النظام", "System administrator", true),
            (RoleCode.Quality, "مسؤول الجودة والاعتماد", "Quality officer", true),
            (RoleCode.Dean, "عميد الكلية", "Dean", true),
            (RoleCode.Instructor, "محاضر الدورة", "Instructor", false),
            (RoleCode.Member, "عضو / متدرب", "Member", true),
            (RoleCode.ExternalInstructor, "مدرب خارجي", "External instructor", false)
        };
        var existing = await db.Roles.Select(x => x.Code).ToListAsync(ct);
        foreach (var (code, ar, en, assignable) in roles)
        {
            if (existing.Contains(code))
            {
                if (code == RoleCode.Member)
                {
                    var member = await db.Roles.SingleAsync(x => x.Code == RoleCode.Member, ct);
                    if (member.NameAr == "عضو هيئة التدريس / متدرب") member.NameAr = ar;
                }
                continue;
            }
            db.Roles.Add(new Role { Code = code, NameAr = ar, NameEn = en,
                IsSystem = true, IsAssignable = assignable, SortOrder = Array.FindIndex(roles, x => x.Code == code) });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedPermissionsAsync(CancellationToken ct)
    {
        var catalog = ReadJson<List<PermissionSeed>>("permissions.json");
        var existing = await db.Permissions.IgnoreQueryFilters().ToDictionaryAsync(x => x.PermKey, ct);
        foreach (var item in catalog)
        {
            if (existing.ContainsKey(item.Key)) continue;
            var permission = new Permission
            {
                PermKey = item.Key, GroupKey = item.Group,
                Kind = Enum.Parse<PermissionKind>(item.Kind),
                Scope = Enum.Parse<PermissionScope>(item.Scope),
                NameAr = item.NameAr, NameEn = item.NameEn,
                SortOrder = catalog.IndexOf(item), IsActive = true
            };
            db.Permissions.Add(permission);
            existing[item.Key] = permission;
        }
        await db.SaveChangesAsync(ct);
        var roles = await db.Roles.ToDictionaryAsync(x => x.Code, ct);
        var grants = await db.RolePermissions.Select(x => new { x.RoleId, x.PermissionId }).ToListAsync(ct);
        var grantSet = grants.Select(x => (x.RoleId, x.PermissionId)).ToHashSet();
        foreach (var item in catalog)
        foreach (string roleName in item.Roles)
        {
            Role role = roles[Enum.Parse<RoleCode>(roleName)];
            Permission permission = existing[item.Key];
            if (grantSet.Add((role.Id, permission.Id)))
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedSettingsAsync(CancellationToken ct)
    {
        var catalog = ReadJson<List<SettingSeed>>("settings.json");
        var existing = await db.SystemSettings.Select(x => x.SettingKey).ToListAsync(ct);
        foreach (var item in catalog)
        {
            if (existing.Contains(item.Key)) continue;
            db.SystemSettings.Add(new SystemSetting
            {
                SettingKey = item.Key, GroupKey = item.Group,
                ValueType = Enum.Parse<SettingValueType>(item.Type),
                Value = item.Value, DefaultValue = item.DefaultValue,
                MinValue = item.Min, MaxValue = item.Max,
                LabelAr = item.LabelAr, LabelEn = item.LabelEn,
                DescriptionAr = item.DescriptionAr, DescriptionEn = item.DescriptionEn,
                IsSecret = item.IsSecret, SortOrder = item.SortOrder
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedPasswordPolicyAsync(CancellationToken ct)
    {
        if (await db.PasswordPolicys.AnyAsync(ct)) return;
        db.PasswordPolicys.Add(new PasswordPolicy
        {
            Id = 1, MinLength = 15, MaxLength = 128,
            AllowSpaces = true, AllowArabic = true,
            BlockCommon = true, BlockContext = true
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedHintsAsync(CancellationToken ct)
    {
        var catalog = ReadJson<List<FieldHintSeed>>("field-hints.ar.json");
        var existing = await db.FieldHints.IgnoreQueryFilters()
            .Select(x => new { x.FieldKey, x.ContextKey }).ToListAsync(ct);
        var keys = existing.Select(x => (x.FieldKey, x.ContextKey)).ToHashSet();
        foreach (var item in catalog)
        {
            if (!keys.Add((item.FieldKey, item.ContextKey))) continue;
            db.FieldHints.Add(new FieldHint
            {
                FieldKey = item.FieldKey, ContextKey = item.ContextKey,
                HintAr = item.HintAr, HintEn = item.HintEn,
                IsEnabled = true, IsActive = true, CreatedAt = clock.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedBlockedPasswordsAsync(CancellationToken ct)
    {
        // Seed only on first initialization so administrator removals survive restarts.
        if (await db.BlockedPasswords.AnyAsync(ct) ||
            await db.AuditLogs.AnyAsync(x => x.ActionCode == "password_policy.block_remove", ct)) return;
        using var reader = ReadResource("common-passwords.txt");
        var expected = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            string normalized = line.Trim().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
            if (!string.IsNullOrEmpty(normalized) && normalized.Length <= 200) expected.Add(normalized);
        }
        var existing = await db.BlockedPasswords.Select(x => x.PasswordNormalized).ToListAsync(ct);
        expected.ExceptWith(existing);
        foreach (string word in expected)
            db.BlockedPasswords.Add(new BlockedPassword { PasswordNormalized = word, Source = "Seed" });
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedOrganizationAsync(CancellationToken ct)
    {
        var existing = await db.UniversitySides.ToDictionaryAsync(x => x.SideCode, ct);
        foreach (var (code, name) in UniversityOrganizationRules.SideNames)
        {
            if (existing.TryGetValue(code, out var side)) side.NameAr = name;
            else db.UniversitySides.Add(new UniversitySide
            {
                SideCode = code, NameAr = name, IsImported = code == 320
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedTracksAsync(CancellationToken ct)
    {
        string[] names =
        [
            "محور التطوير التقني والرقمي",
            "محور طرائق التدريس والقياس والتقويم",
            "محور البحث العلمي والنشر الدولي",
            "محور القيادة المؤسسية والموارد البشرية (HR)"
        ];
        var existing = await db.TrainingTracks.IgnoreQueryFilters().Select(x => x.NameAr).ToListAsync(ct);
        for (int i = 0; i < names.Length; i++)
            if (!existing.Contains(names[i]))
                db.TrainingTracks.Add(new TrainingTrack
                {
                    NameAr = names[i], SortOrder = i, CreatedAt = clock.UtcNow, IsActive = true
                });
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedEvaluationFormAsync(CancellationToken ct)
    {
        if (await db.EvaluationFormVersions.AnyAsync(x => x.VersionNo == 1, ct)) return;
        var form = new EvaluationFormVersion
        {
            VersionNo = 1, Status = FormVersionStatus.Published,
            TitleAr = "نموذج تقييم الدورة", TitleEn = "Course evaluation form",
            PublishedAt = clock.UtcNow, CreatedAt = clock.UtcNow
        };
        db.EvaluationFormVersions.Add(form);
        await db.SaveChangesAsync(ct);
        string[] questions =
        [
            "مدى وضوح أهداف الدورة", "مدى ملاءمة المحتوى لاحتياجاتي",
            "تمكّن المحاضر من المادة", "أسلوب المحاضر في العرض والتفاعل",
            "جودة المواد التدريبية والتنظيم", "تقييمي العام للدورة"
        ];
        string[] labels = ["ممتاز", "جيد جداً", "جيد", "مقبول", "ضعيف"];
        for (int i = 0; i < questions.Length; i++)
        {
            var question = new EvaluationQuestion
            {
                VersionId = form.Id, QuestionKey = Guid.NewGuid(), SortOrder = i,
                TextAr = questions[i]
            };
            db.EvaluationQuestions.Add(question);
            await db.SaveChangesAsync(ct);
            for (int j = 0; j < labels.Length; j++)
                db.EvaluationOptions.Add(new EvaluationOption
                {
                    QuestionId = question.Id, SortOrder = j, LabelAr = labels[j], Score = (short)(5 - j)
                });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedProfessionalCategoriesAsync(CancellationToken ct)
    {
        string[] names =
        [
            "تقنية المعلومات والحوسبة السحابية", "الأمن السيبراني", "إدارة المشاريع والأعمال",
            "البيانات والذكاء الاصطناعي", "التدريس والتعليم الإلكتروني", "البحث العلمي والنشر",
            "اللغات", "أخرى"
        ];
        var existing = await db.ProfessionalCertCategorys.IgnoreQueryFilters().Select(x => x.NameAr).ToListAsync(ct);
        for (int i = 0; i < names.Length; i++)
            if (!existing.Contains(names[i]))
                db.ProfessionalCertCategorys.Add(new ProfessionalCertCategory
                {
                    NameAr = names[i], SortOrder = i, CreatedAt = clock.UtcNow, IsActive = true
                });
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedBootstrapAdminAsync(CancellationToken ct)
    {
        long adminRoleId = await db.Roles.Where(x => x.Code == RoleCode.Admin).Select(x => x.Id).SingleAsync(ct);
        bool hasAdmin = await db.UserRoles.AnyAsync(x => x.RoleId == adminRoleId && x.IsActive && x.User!.IsActive, ct);
        if (hasAdmin) return;
        string? login = Environment.GetEnvironmentVariable("ZU_BOOTSTRAP_ADMIN_LOGIN");
        string? password = Environment.GetEnvironmentVariable("ZU_BOOTSTRAP_ADMIN_PASSWORD");
        string? name = Environment.GetEnvironmentVariable("ZU_BOOTSTRAP_ADMIN_NAME");
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(name))
            return;
        if (await db.Users.IgnoreQueryFilters().AnyAsync(x => x.LoginName == login, ct)) return;
        var user = new User
        {
            AccountType = AccountType.Local, LoginName = login,
            FullNameAr = name, PasswordHash = passwordHasher.Hash(password),
            PasswordEnc = string.Empty, MustChangePassword = false,
            FirstLoginCompletedAt = clock.UtcNow, SecurityStamp = Guid.NewGuid(),
            PreferredLanguage = UiLanguage.Ar, CredentialsEmailStatus = CredentialsEmailStatus.NotSent,
            SearchText = ArabicSearchNormalizer.Normalize($"{name} {login}"),
            CreatedAt = clock.UtcNow, IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id, RoleId = adminRoleId, IsActive = true, CreatedAt = clock.UtcNow
        });
        db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = clock.UtcNow, Category = AuditCategory.System,
            ActionCode = "seed.bootstrap_admin", ActorUserId = null,
            EntityType = "User", EntityId = user.Id,
            SummaryAr = "إنشاء حساب المسؤول المحلي الأول", SummaryEn = "Bootstrap local administrator created",
            Success = true
        });
        await db.SaveChangesAsync(ct);
    }

    private static T ReadJson<T>(string fileName)
    {
        using var reader = ReadResource(fileName);
        string text = reader.ReadToEnd();
        return JsonSerializer.Deserialize<T>(text, JsonOptions)
            ?? throw new InvalidOperationException($"Invalid seed resource: {fileName}");
    }

    private static StreamReader ReadResource(string fileName)
    {
        string name = $"ZuLms.Infrastructure.Persistence.Seed.{fileName}";
        Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing seed resource: {fileName}");
        return new StreamReader(stream);
    }

    private sealed class PermissionSeed
    {
        public string Key { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = [];
    }

    private sealed class SettingSeed
    {
        public string Key { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public string LabelAr { get; set; } = string.Empty;
        public string? LabelEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string? DescriptionEn { get; set; }
        public bool IsSecret { get; set; }
        public int SortOrder { get; set; }
    }

    private sealed class FieldHintSeed
    {
        public string FieldKey { get; set; } = string.Empty;
        public string ContextKey { get; set; } = string.Empty;
        public string HintAr { get; set; } = string.Empty;
        public string? HintEn { get; set; }
    }
}

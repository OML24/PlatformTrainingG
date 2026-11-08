using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Entities.Sync;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Sync;

public sealed class UniversityProvisioningService(IAppDbContext db, IClock clock,
    IPasswordHasher hasher, IEncryptionService encryption, ISettingsService settings)
{
    public Task<ProvisioningStats> ApplyAsync(long runId, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            var importedSides = (await db.UniversitySides.AsNoTracking()
                .Where(x => x.IsImported).Select(x => x.SideCode).ToListAsync(ct)).ToHashSet();
            var employees = await db.Employees.AsNoTracking()
                .Where(x => !x.SourceMissing && importedSides.Contains(x.SideCode))
                .OrderBy(x => x.SideCode).ThenBy(x => x.EmployeeCode).ToListAsync(ct);
            var users = await db.Users.IgnoreQueryFilters().ToListAsync(ct);
            var byEmployeeId = users.Where(x => x.EmployeeId.HasValue)
                .ToDictionary(x => x.EmployeeId!.Value);
            var usedLogins = users.Select(x => x.LoginName).ToHashSet(StringComparer.Ordinal);
            var policy = await db.PasswordPolicys.AsNoTracking().SingleAsync(ct);
            var blocked = await db.BlockedPasswords.AsNoTracking()
                .Select(x => x.PasswordNormalized).ToListAsync(ct);
            string shared = await settings.GetAsync<string>("auth.shared_initial_password", ct);
            long memberRoleId = await db.Roles.Where(x => x.Code == RoleCode.Member)
                .Select(x => x.Id).SingleAsync(ct);
            int created = 0, updated = 0, collisions = 0;
            foreach (var employee in employees)
            {
                string search = ArabicSearchNormalizer.Normalize(
                    $"{employee.FullNameAr} {employee.FullNameEn} {employee.EmployeeCode}");
                if (byEmployeeId.TryGetValue(employee.Id, out var existing))
                {
                    if (existing.FullNameAr != employee.FullNameAr ||
                        existing.FullNameEn != employee.FullNameEn || existing.SearchText != search)
                    {
                        existing.FullNameAr = employee.FullNameAr;
                        existing.FullNameEn = employee.FullNameEn;
                        existing.SearchText = search;
                        updated++;
                    }
                    continue;
                }
                string login = employee.EmployeeCode.ToString(CultureInfo.InvariantCulture);
                if (employee.EmployeeCode <= 0 || !usedLogins.Add(login))
                {
                    collisions++;
                    string entityKey = $"{employee.SideCode}:{employee.EmployeeCode}";
                    bool open = await db.SyncReviewItems.AnyAsync(x => x.EntityType == "Employee" &&
                        x.EntityKey == entityKey && x.Reason == SyncReviewReason.DuplicateLoginName &&
                        x.ResolvedAt == null, ct);
                    if (!open) db.SyncReviewItems.Add(new SyncReviewItem
                    {
                        EntityType = "Employee", EntityKey = entityKey,
                        Reason = SyncReviewReason.DuplicateLoginName,
                        FirstSeenRunId = runId, Origin = SyncReviewOrigin.Provisioning
                    });
                    continue;
                }
                string[] context = employee.FullNameAr.Split(' ',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Concat([login, shared]).ToArray();
                string password = InitialPasswordRules.Generate(policy, blocked, context);
                string encrypted = encryption.Encrypt(password);
                var user = new User
                {
                    AccountType = AccountType.Employee, EmployeeId = employee.Id,
                    LoginName = login, FullNameAr = employee.FullNameAr,
                    FullNameEn = employee.FullNameEn, SearchText = search,
                    PasswordHash = hasher.Hash(password), PasswordEnc = encrypted,
                    InitialPasswordEnc = encrypted, MustChangePassword = true,
                    PasswordChangedAt = clock.UtcNow, SecurityStamp = Guid.NewGuid(),
                    PreferredLanguage = UiLanguage.Ar,
                    CredentialsEmailStatus = CredentialsEmailStatus.NotSent,
                    CreatedAt = clock.UtcNow, IsActive = true
                };
                db.Users.Add(user);
                db.UserRoles.Add(new UserRole
                {
                    User = user, RoleId = memberRoleId, IsActive = true,
                    CreatedAt = clock.UtcNow
                });
                created++;
            }
            await db.SaveChangesAsync(ct);
            return new ProvisioningStats(created, updated, collisions);
        }, ct);
}

public sealed record ProvisioningStats(int Created, int Updated, int LoginCollisions);

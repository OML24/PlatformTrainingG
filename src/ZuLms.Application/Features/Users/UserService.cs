using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Enums;
using ZuLms.Application.Features.Users.Dtos;
using ZuLms.Application.Features.Permissions;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Users;

public sealed class UserService(IAppDbContext db, IClock clock, IPermissionService permissions,
    IUserStateCache userState, IAuditLogger audit, ISettingsService settings,
    IPasswordHasher hasher) : IUserService
{
    public async Task<Result<IReadOnlyList<UserPermissionDto>>> ListPermissionsAsync(long actorUserId,
        long targetUserId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "user.permissions.override", ct))
            return Result<IReadOnlyList<UserPermissionDto>>.Failure("FORBIDDEN");
        if (!await db.Users.IgnoreQueryFilters().AnyAsync(x => x.Id == targetUserId, ct))
            return Result<IReadOnlyList<UserPermissionDto>>.Failure("USER_NOT_FOUND");
        var catalog = await db.Permissions.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.GroupKey).ThenBy(x => x.SortOrder)
            .Select(x => new { x.Id, x.PermKey, x.GroupKey, x.NameAr, x.NameEn })
            .ToListAsync(ct);
        var overrides = await db.UserPermissionOverrides.AsNoTracking()
            .Where(x => x.UserId == targetUserId)
            .ToDictionaryAsync(x => x.PermissionId, x => x.Mode, ct);
        var effective = await permissions.GetAccountAsync(targetUserId, ct);
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking().Where(x => x.Id == targetUserId)
            .Select(x => new { x.AccountType }).SingleAsync(ct);
        var roleIds = await db.Roles.AsNoTracking().Where(x => effective.Roles.Contains(x.Code))
            .Select(x => x.Id).ToListAsync(ct);
        var roleSources = await db.UserRoles.AsNoTracking().Where(x => x.UserId == targetUserId && x.IsActive)
            .Select(x => new RolePermissionSourceDto(x.Role!.Code, x.ScopeCenterId,
                x.ScopeCenter == null ? null : x.ScopeCenter.NameAr)).ToListAsync(ct);
        bool instructs = await db.Courses.AsNoTracking().AnyAsync(x => x.InstructorUserId == targetUserId &&
            (x.Status == CourseStatus.Approved || x.Status == CourseStatus.Finished), ct);
        if (instructs)
        {
            roleIds.Add(await db.Roles.Where(x => x.Code == RoleCode.Instructor).Select(x => x.Id).SingleAsync(ct));
            roleSources.Add(new RolePermissionSourceDto(RoleCode.Instructor, null, null));
        }
        if (user.AccountType == AccountType.ExternalInstructor)
        {
            roleIds.Add(await db.Roles.Where(x => x.Code == RoleCode.ExternalInstructor).Select(x => x.Id).SingleAsync(ct));
            roleSources.Add(new RolePermissionSourceDto(RoleCode.ExternalInstructor, null, null));
        }
        roleIds = roleIds.Distinct().ToList();
        var rolePermissions = await db.RolePermissions.AsNoTracking().Where(x => roleIds.Contains(x.RoleId))
            .Select(x => new { x.PermissionId, x.Role!.Code }).ToListAsync(ct);
        var inheritedKeys = catalog.Where(x => rolePermissions.Any(r => r.PermissionId == x.Id))
            .Select(x => x.PermKey).ToHashSet(StringComparer.Ordinal);
        PermissionSetRules.ApplyAccountType(user.AccountType, inheritedKeys);
        var effectiveKeys = inheritedKeys.ToHashSet(StringComparer.Ordinal);
        foreach (var (permissionId, mode) in overrides)
        {
            var key = catalog.FirstOrDefault(x => x.Id == permissionId)?.PermKey;
            if (key is null) continue;
            if (mode == OverrideMode.Allow) effectiveKeys.Add(key);
            else effectiveKeys.Remove(key);
        }
        PermissionSetRules.ApplyAccountType(user.AccountType, effectiveKeys);
        IReadOnlyList<UserPermissionDto> rows = catalog.Select(x => new UserPermissionDto(
            x.PermKey, x.GroupKey, x.NameAr, x.NameEn,
            overrides.TryGetValue(x.Id, out var mode) ? mode : null,
            effectiveKeys.Contains(x.PermKey), inheritedKeys.Contains(x.PermKey),
            rolePermissions.Where(r => r.PermissionId == x.Id)
                .SelectMany(r => roleSources.Where(source => source.Role == r.Code))
                .Distinct().OrderBy(source => source.Role)
                .ThenBy(source => source.ScopeCenterNameAr).ToList())).ToList();
        return Result<IReadOnlyList<UserPermissionDto>>.Success(rows);
    }

    public async Task<Result<IReadOnlyList<FacultyOptionDto>>> ListFacultiesAsync(long actorUserId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "user.roles.assign", ct) &&
            !await permissions.HasAsync(actorUserId, "page.shared.directory", ct))
            return Result<IReadOnlyList<FacultyOptionDto>>.Failure("FORBIDDEN");
        var rows = await db.Centers.AsNoTracking().Where(x => x.IsFaculty && x.IsActive)
            .OrderBy(x => x.NameAr).Select(x => new FacultyOptionDto(x.Id, x.NameAr, x.NameEn))
            .ToListAsync(ct);
        rows.Insert(0, new FacultyOptionDto(0, "الإداريون", "Administrative staff"));
        return Result<IReadOnlyList<FacultyOptionDto>>.Success(rows);
    }
    public Task<Result<CredentialsDirectoryPageDto>> SearchCredentialsAsync(long actorUserId,
        string? search, CredentialsDirectoryFilter filter, int page, CancellationToken ct) =>
        SearchCredentialsAsync(actorUserId, search, filter, null, null, null, null, page, ct);

    public async Task<Result<CredentialsDirectoryPageDto>> SearchCredentialsAsync(long actorUserId,
        string? search, CredentialsDirectoryFilter filter, AccountType? accountType,
        RoleCode? role, long? centerId, bool? isActive, int page, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.credentials", ct))
            return Result<CredentialsDirectoryPageDto>.Failure("FORBIDDEN");
        if (!Enum.IsDefined(filter) || accountType.HasValue && !Enum.IsDefined(accountType.Value) ||
            role.HasValue && !Enum.IsDefined(role.Value) || centerId is < 0 || page < 1)
            return Result<CredentialsDirectoryPageDto>.Failure("QUERY_INVALID");
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (page > int.MaxValue / pageSize) return Result<CredentialsDirectoryPageDto>.Failure("QUERY_INVALID");
        IQueryable<User> query = db.Users.IgnoreQueryFilters().AsNoTracking();
        string term = ArabicSearchNormalizer.Normalize(search);
        if (term.Length > 0) query = query.Where(x => x.SearchText.Contains(term) ||
            x.LoginName.ToLower().Contains(term));
        if (accountType.HasValue) query = query.Where(x => x.AccountType == accountType.Value);
        if (centerId == 0) query = query.Where(x => x.Employee != null && !x.Employee.IsAcademic);
        else if (centerId.HasValue) query = query.Where(x => x.Employee != null && x.Employee.CenterId == centerId.Value);
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        if (role.HasValue)
        {
            RoleCode selectedRole = role.Value;
            if (selectedRole == RoleCode.Instructor)
                query = query.Where(x => db.Courses.Any(c => c.InstructorUserId == x.Id &&
                    (c.Status == CourseStatus.Approved || c.Status == CourseStatus.Finished)));
            else if (selectedRole == RoleCode.ExternalInstructor)
                query = query.Where(x => x.AccountType == AccountType.ExternalInstructor);
            else query = query.Where(x => db.UserRoles.Any(ur => ur.UserId == x.Id && ur.IsActive &&
                ur.Role!.Code == selectedRole));
        }
        query = filter switch
        {
            CredentialsDirectoryFilter.NotSent => query.Where(x => x.CredentialsEmailStatus == CredentialsEmailStatus.NotSent),
            CredentialsDirectoryFilter.Sent => query.Where(x => x.CredentialsEmailStatus == CredentialsEmailStatus.Sent),
            CredentialsDirectoryFilter.Failed => query.Where(x => x.CredentialsEmailStatus == CredentialsEmailStatus.Failed),
            CredentialsDirectoryFilter.Uncertain => query.Where(x => x.CredentialsEmailStatus == CredentialsEmailStatus.Uncertain),
            CredentialsDirectoryFilter.NoEmail => query.Where(x => (x.EmailOverride ?? x.Employee!.SourceEmail) == null ||
                (x.EmailOverride ?? x.Employee!.SourceEmail) == ""),
            CredentialsDirectoryFilter.FirstLoginPending => query.Where(x => x.AccountType == AccountType.Employee &&
                x.FirstLoginCompletedAt == null),
            CredentialsDirectoryFilter.Disabled => query.Where(x => !x.IsActive),
            _ => query
        };
        int total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.FullNameAr).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new CredentialsDirectoryRowDto(x.Id, x.Version, x.FullNameAr,
                x.LoginName, x.AccountType, x.Employee == null || x.Employee.Center == null
                    ? null : x.Employee.Center.NameAr, x.IsActive,
                x.FirstLoginCompletedAt != null, x.EmailOverride ?? x.Employee!.SourceEmail,
                x.CredentialsEmailStatus, x.LastLoginAt,
                db.EmailOutboxs.Any(m => m.Kind == EmailKind.InitialCredentials &&
                    m.RelatedUserId == x.Id && m.Status == OutboxStatus.Pending),
                x.Employee == null ? null : x.Employee.EmpStatus,
                x.Employee == null ? null : x.Employee.EmpStatusDesc))
            .ToListAsync(ct);
        return Result<CredentialsDirectoryPageDto>.Success(new(page, pageSize, total, rows));
    }

    public async Task<Result<long>> CreateLocalAsync(long actorUserId, string loginName,
        string fullNameAr, string? fullNameEn, string password, UiLanguage language, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "user.create", ct))
            return Result<long>.Failure("FORBIDDEN");
        string login = LoginNameRules.NormalizeLogin(loginName);
        string name = fullNameAr.Trim();
        string? englishName = string.IsNullOrWhiteSpace(fullNameEn) ? null : fullNameEn.Trim();
        if (!Regex.IsMatch(login, "^[A-Za-z0-9_.-]{3,20}$")) return Result<long>.Failure("LOGIN_INVALID");
        int externalMin = await settings.GetAsync<int>("auth.external_login_min", ct);
        int externalMax = await settings.GetAsync<int>("auth.external_login_max", ct);
        if (int.TryParse(login, out int numericLogin) && numericLogin >= externalMin && numericLogin <= externalMax)
            return Result<long>.Failure("LOGIN_RESERVED");
        if (name.Length is < 2 or > 400 || englishName?.Length > 400)
            return Result<long>.Failure("NAME_INVALID");
        if (!Enum.IsDefined(language)) return Result<long>.Failure("LANGUAGE_INVALID");
        var policy = await db.PasswordPolicys.AsNoTracking().SingleAsync(ct);
        var blocked = await db.BlockedPasswords.AsNoTracking()
            .Select(x => x.PasswordNormalized).ToListAsync(ct);
        var errors = PasswordPolicyRules.Validate(password, policy, blocked,
            name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Concat([login]));
        if (errors.Count > 0) return Result<long>.Failure(errors[0]);
        return await db.InSerializableTransactionAsync(async () =>
        {
            if (await db.Users.IgnoreQueryFilters().AnyAsync(x => x.LoginName == login, ct))
                return Result<long>.Failure("LOGIN_EXISTS");
            long adminRoleId = await db.Roles.Where(x => x.Code == RoleCode.Admin)
                .Select(x => x.Id).SingleAsync(ct);
            var user = new User
            {
                AccountType = AccountType.Local,
                LoginName = login,
                FullNameAr = name,
                FullNameEn = englishName,
                PasswordHash = hasher.Hash(password),
                PasswordEnc = string.Empty,
                FirstLoginCompletedAt = clock.UtcNow,
                PasswordChangedAt = clock.UtcNow,
                PasswordChangedBy = actorUserId,
                SecurityStamp = Guid.NewGuid(),
                PreferredLanguage = language,
                CredentialsEmailStatus = CredentialsEmailStatus.NotSent,
                SearchText = ArabicSearchNormalizer.Normalize($"{name} {englishName} {login}"),
                CreatedAt = clock.UtcNow,
                CreatedByUserId = actorUserId,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = adminRoleId,
                IsActive = true,
                CreatedAt = clock.UtcNow,
                CreatedByUserId = actorUserId
            });
            await audit.LogAsync(new AuditEntry(AuditCategory.User, "user.create_local",
                "إنشاء حساب محلي", "Local administrator created", "User", user.Id,
                user.FullNameAr, ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result<long>.Success(user.Id);
        }, ct);
    }
    public async Task<Result<UserDetailsDto>> GetDetailsAsync(long actorUserId, long targetUserId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.users", ct) &&
            !await permissions.HasAsync(actorUserId, "page.admin.credentials", ct))
            return Result<UserDetailsDto>.Failure("FORBIDDEN");
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking().Include(x => x.Employee)
            .SingleOrDefaultAsync(x => x.Id == targetUserId, ct);
        if (user is null) return Result<UserDetailsDto>.Failure("USER_NOT_FOUND");
        var roles = await db.UserRoles.AsNoTracking().Where(x => x.UserId == targetUserId && x.IsActive)
            .Select(x => new UserRoleDto(x.Role!.Code, x.ScopeCenterId)).ToListAsync(ct);
        string? email = user.EmailOverride ?? user.Employee?.SourceEmail;
        return Result<UserDetailsDto>.Success(new UserDetailsDto(user.Id, user.Version,
            user.LoginName, user.FullNameAr, user.FullNameEn, user.AccountType,
            user.IsActive, email, user.EmailOverride, user.PreferredLanguage,
            user.AccessOverrideAllow, user.AccessOverrideReason, roles));
    }

    public async Task<Result> DeactivateAsync(long actorUserId, long targetUserId,
        uint expectedVersion, string reason, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "user.deactivate", ct))
            return Result.Failure("FORBIDDEN");
        if (string.IsNullOrWhiteSpace(reason)) return Result.Failure("REASON_REQUIRED");
        return await db.InSerializableTransactionAsync(async () =>
        {
            User? user = await db.Users.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.Id == targetUserId, ct);
            if (user is null) return Result.Failure("USER_NOT_FOUND");
            if (user.Version != expectedVersion) return Result.Failure("CONFLICT");
            if (!user.IsActive) return Result.Failure("USER_ALREADY_DISABLED");
            if (await IsLastAdminAsync(user.Id, ct)) return Result.Failure("LAST_ADMIN");
            user.IsActive = false;
            user.DeactivatedAt = clock.UtcNow;
            user.DeactivatedByUserId = actorUserId;
            user.DeactivationReason = reason.Trim();
            user.SecurityStamp = Guid.NewGuid();
            var tokens = await db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var token in tokens) { token.RevokedAt = clock.UtcNow; token.RevokedReason = "UserDeactivated"; }
            await audit.LogAsync(new AuditEntry(AuditCategory.User, "user.deactivate",
                "تعطيل حساب مستخدم", "User account deactivated", "User", user.Id, user.FullNameAr,
                BeforeJson: JsonSerializer.Serialize(new { isActive = true }),
                AfterJson: JsonSerializer.Serialize(new { isActive = false, reason = reason.Trim() }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            Invalidate(user.Id);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> RestoreAsync(long actorUserId, long targetUserId,
        uint expectedVersion, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "user.restore", ct))
            return Result.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            User? user = await db.Users.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.Id == targetUserId, ct);
            if (user is null) return Result.Failure("USER_NOT_FOUND");
            if (user.Version != expectedVersion) return Result.Failure("CONFLICT");
            if (user.IsActive) return Result.Failure("USER_ALREADY_ACTIVE");
            user.IsActive = true;
            user.DeactivatedAt = null;
            user.DeactivatedByUserId = null;
            user.DeactivationReason = null;
            user.SecurityStamp = Guid.NewGuid();
            await audit.LogAsync(new AuditEntry(AuditCategory.User, "user.restore",
                "استعادة حساب مستخدم", "User account restored", "User", user.Id, user.FullNameAr,
                BeforeJson: JsonSerializer.Serialize(new { isActive = false }),
                AfterJson: JsonSerializer.Serialize(new { isActive = true }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            Invalidate(user.Id);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> AssignRoleAsync(long actorUserId, long targetUserId, RoleCode roleCode,
        long? scopeCenterId, bool enabled, uint expectedVersion, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "user.roles.assign", ct))
            return Result.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            User? user = await db.Users.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.Id == targetUserId, ct);
            if (user is null) return Result.Failure("USER_NOT_FOUND");
            if (user.Version != expectedVersion) return Result.Failure("CONFLICT");
            Role? role = await db.Roles.SingleOrDefaultAsync(x => x.Code == roleCode, ct);
            if (role is null || !role.IsAssignable) return Result.Failure("ROLE_NOT_ASSIGNABLE");
            if (roleCode == RoleCode.Dean)
            {
                if (scopeCenterId is null || !await db.Centers.AnyAsync(x => x.Id == scopeCenterId && x.IsFaculty, ct))
                    return Result.Failure("DEAN_FACULTY_REQUIRED");
            }
            else if (scopeCenterId is not null) return Result.Failure("ROLE_SCOPE_INVALID");
            if (user.AccountType == AccountType.ExternalInstructor && roleCode == RoleCode.Member)
                return Result.Failure("ROLE_NOT_ASSIGNABLE");
            if (user.AccountType == AccountType.Local && roleCode != RoleCode.Admin)
                return Result.Failure("ROLE_NOT_ASSIGNABLE");
            UserRole? assignment = await db.UserRoles.SingleOrDefaultAsync(x => x.UserId == targetUserId &&
                x.RoleId == role.Id && x.ScopeCenterId == scopeCenterId && x.IsActive, ct);
            if (enabled && assignment is not null || !enabled && assignment is null) return Result.Success();
            if (!enabled && roleCode == RoleCode.Admin && await IsLastAdminAsync(targetUserId, ct))
                return Result.Failure("LAST_ADMIN");
            if (enabled)
                db.UserRoles.Add(new UserRole
                {
                    UserId = targetUserId,
                    RoleId = role.Id,
                    ScopeCenterId = scopeCenterId,
                    IsActive = true,
                    CreatedAt = clock.UtcNow,
                    CreatedByUserId = actorUserId
                });
            else
            {
                assignment!.IsActive = false;
                assignment.RevokedAt = clock.UtcNow;
                assignment.RevokedBy = actorUserId;
            }
            user.SecurityStamp = Guid.NewGuid();
            await audit.LogAsync(new AuditEntry(AuditCategory.Permission, "user.roles.assign",
                "تعديل دور مستخدم", "User role changed", "User", user.Id, user.FullNameAr,
                BeforeJson: JsonSerializer.Serialize(new { role = roleCode.ToString(), enabled = !enabled, scopeCenterId }),
                AfterJson: JsonSerializer.Serialize(new { role = roleCode.ToString(), enabled, scopeCenterId }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            Invalidate(user.Id);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> SetPermissionOverrideAsync(long actorUserId, long targetUserId,
        string permissionKey, OverrideMode? mode, uint expectedVersion, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "user.permissions.override", ct))
            return Result.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            User? user = await db.Users.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.Id == targetUserId, ct);
            if (user is null) return Result.Failure("USER_NOT_FOUND");
            if (user.Version != expectedVersion) return Result.Failure("CONFLICT");
            Permission? permission = await db.Permissions.SingleOrDefaultAsync(x =>
                x.PermKey == permissionKey && x.IsActive, ct);
            if (permission is null) return Result.Failure("PERMISSION_NOT_FOUND");
            bool critical = permissionKey is "page.admin.users" or "user.permissions.override" or "user.roles.assign";
            if (mode == OverrideMode.Deny && critical &&
                (actorUserId == targetUserId || await IsLastAdminAsync(targetUserId, ct)))
                return Result.Failure("LAST_ADMIN_GUARD");
            var existing = await db.UserPermissionOverrides.SingleOrDefaultAsync(x =>
                x.UserId == targetUserId && x.PermissionId == permission.Id, ct);
            if (existing?.Mode == mode || existing is null && mode is null) return Result.Success();
            string? before = existing?.Mode.ToString();
            if (mode is null) db.UserPermissionOverrides.Remove(existing!);
            else if (existing is null)
                db.UserPermissionOverrides.Add(new UserPermissionOverride
                {
                    UserId = targetUserId,
                    PermissionId = permission.Id,
                    Mode = mode.Value,
                    CreatedAt = clock.UtcNow,
                    CreatedByUserId = actorUserId
                });
            else
            {
                existing.Mode = mode.Value;
                existing.UpdatedAt = clock.UtcNow;
                existing.UpdatedByUserId = actorUserId;
            }
            user.SecurityStamp = Guid.NewGuid();
            await audit.LogAsync(new AuditEntry(AuditCategory.Permission, "user.permissions.override",
                "تعديل استثناء صلاحية", "User permission override changed", "User", user.Id, user.FullNameAr,
                BeforeJson: JsonSerializer.Serialize(new { permissionKey, mode = before }),
                AfterJson: JsonSerializer.Serialize(new { permissionKey, mode = mode?.ToString() }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            Invalidate(user.Id);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> EditAsync(long actorUserId, long targetUserId, uint expectedVersion,
        string? fullNameEn, string? emailOverride, UiLanguage language, bool accessOverrideAllow,
        string? accessOverrideReason, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "user.edit", ct))
            return Result.Failure("FORBIDDEN");
        if (!Enum.IsDefined(language)) return Result.Failure("LANGUAGE_INVALID");
        string? email = string.IsNullOrWhiteSpace(emailOverride) ? null : emailOverride.Trim().ToLowerInvariant();
        if (email is not null && (email.Length > 254 || !new EmailAddressAttribute().IsValid(email)))
            return Result.Failure("EMAIL_INVALID");
        string? englishName = string.IsNullOrWhiteSpace(fullNameEn) ? null : fullNameEn.Trim();
        if (englishName?.Length > 300) return Result.Failure("NAME_TOO_LONG");
        if (accessOverrideAllow && string.IsNullOrWhiteSpace(accessOverrideReason))
            return Result.Failure("REASON_REQUIRED");
        User? user = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == targetUserId, ct);
        if (user is null) return Result.Failure("USER_NOT_FOUND");
        if (user.Version != expectedVersion) return Result.Failure("CONFLICT");
        if (accessOverrideAllow && user.AccountType != AccountType.Employee)
            return Result.Failure("ACCESS_OVERRIDE_NOT_ALLOWED");
        var before = new
        {
            user.FullNameEn,
            user.EmailOverride,
            user.PreferredLanguage,
            user.AccessOverrideAllow,
            user.AccessOverrideReason
        };
        bool accessChanged = user.AccessOverrideAllow != accessOverrideAllow;
        user.FullNameEn = englishName;
        user.EmailOverride = email;
        user.PreferredLanguage = language;
        user.AccessOverrideAllow = accessOverrideAllow;
        user.AccessOverrideReason = accessOverrideAllow ? accessOverrideReason!.Trim() : null;
        user.AccessOverrideBy = accessOverrideAllow ? actorUserId : null;
        user.AccessOverrideAt = accessOverrideAllow ? clock.UtcNow : null;
        if (accessChanged) user.SecurityStamp = Guid.NewGuid();
        await audit.LogAsync(new AuditEntry(AuditCategory.User, "user.edit",
            "تعديل بيانات مستخدم", "User account edited", "User", user.Id, user.FullNameAr,
            BeforeJson: JsonSerializer.Serialize(before),
            AfterJson: JsonSerializer.Serialize(new
            {
                user.FullNameEn,
                user.EmailOverride,
                user.PreferredLanguage,
                user.AccessOverrideAllow,
                user.AccessOverrideReason
            }),
            ActorUserId: actorUserId), ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Failure("CONFLICT"); }
        Invalidate(user.Id);
        return Result.Success();
    }

    private async Task<bool> IsLastAdminAsync(long targetUserId, CancellationToken ct)
    {
        bool targetIsAdmin = await db.UserRoles.AnyAsync(x => x.UserId == targetUserId &&
            x.IsActive && x.Role!.Code == RoleCode.Admin, ct);
        if (!targetIsAdmin) return false;
        int activeAdmins = await db.UserRoles.Where(x => x.IsActive &&
            x.Role!.Code == RoleCode.Admin && x.User!.IsActive)
            .Select(x => x.UserId).Distinct().CountAsync(ct);
        return activeAdmins <= 1;
    }

    private void Invalidate(long userId)
    {
        permissions.Invalidate(userId);
        userState.Invalidate(userId);
    }
}

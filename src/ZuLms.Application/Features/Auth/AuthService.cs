using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Auth;

public sealed class AuthService(
    IAppDbContext db, IClock clock, IPasswordHasher hasher, IEncryptionService encryption,
    ISettingsService settings, IPermissionService permissions, ITokenService tokens,
    IUserStateCache userState, IEmailContentBuilder emailContent,
    IAuditLogger audit) : IAuthService
{
    private static string? dummyHash;

    public async Task<Result<AuthSession>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.LoginName) || string.IsNullOrEmpty(request.Password))
            return Result<AuthSession>.Failure("AUTH_INVALID");
        string login = LoginNameRules.NormalizeLogin(request.LoginName);
        string password = request.Password.Normalize(NormalizationForm.FormC);
        User? user = await db.Users.IgnoreQueryFilters().Include(x => x.Employee)
            .SingleOrDefaultAsync(x => x.LoginName == login, ct);
        if (user is null)
        {
            string dummy = dummyHash ??= hasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            hasher.Verify(password, dummy);
            await AuditFailureAsync(null, login, "AUTH_INVALID", ct);
            await Task.Delay(TimeSpan.FromMilliseconds(400), ct);
            return Result<AuthSession>.Failure("AUTH_INVALID");
        }
        DateTimeOffset now = clock.UtcNow;
        if (user.LockedUntil > now)
        {
            await AuditFailureAsync(user, login, "AUTH_LOCKED", ct);
            return Result<AuthSession>.Failure("AUTH_LOCKED");
        }
        bool first = user.AccountType == AccountType.Employee && user.FirstLoginCompletedAt is null;
        bool sharedUsed = false;
        bool valid;
        if (first)
        {
            string initial = user.InitialPasswordEnc is null ? string.Empty : encryption.Decrypt(user.InitialPasswordEnc);
            string shared = await settings.GetAsync<string>("auth.shared_initial_password", ct);
            valid = FixedEquals(password, initial);
            if (!valid && !string.IsNullOrEmpty(shared))
                valid = sharedUsed = FixedEquals(password, shared);
        }
        else valid = hasher.Verify(password, user.PasswordHash);
        if (!valid)
        {
            user.FailedLoginCount++;
            await AuditFailureAsync(user, login, "AUTH_INVALID", ct);
            // Delay the failing request without locking out someone who knows only the login name.
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(2000,
                200 * (1 << Math.Min(user.FailedLoginCount, 4)))), ct);
            return Result<AuthSession>.Failure("AUTH_INVALID");
        }
        if (!user.IsActive)
        {
            await AuditFailureAsync(user, login, "AUTH_DISABLED", ct);
            return Result<AuthSession>.Failure("AUTH_DISABLED");
        }
        if (user.AccountType == AccountType.Employee && !user.AccessOverrideAllow)
        {
            string codes = await settings.GetAsync<string>("access.eligible_emp_status_codes", ct);
            bool eligible = user.Employee?.EmpStatus is int status &&
                codes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Any(x => int.TryParse(x, out int code) && code == status);
            if (!eligible)
            {
                await AuditFailureAsync(user, login, "AUTH_NOT_ELIGIBLE", ct);
                return Result<AuthSession>.Failure("AUTH_NOT_ELIGIBLE");
            }
        }
        user.FailedLoginCount = 0;
        if (!first && hasher.NeedsRehash(user.PasswordHash))
            user.PasswordHash = hasher.Hash(password);
        user.LockedUntil = null;
        user.LastLoginAt = now;
        bool rotationDue = !first && await RotationDueAsync(user, ct);
        bool limited = first || rotationDue || user.AccountType == AccountType.Employee && user.MustChangePassword;
        AuthSession session = await IssueSessionAsync(user, request.RememberMe, limited, null, null, ct);
        await audit.LogAsync(new AuditEntry(AuditCategory.Auth, first ? "auth.login_first" : "auth.login",
            "تسجيل دخول ناجح", "Successful login", "User", user.Id, user.FullNameAr,
            ActorUserId: user.Id, ActorLogin: login, ActorName: user.FullNameAr), ct);
        if (sharedUsed)
            await audit.LogAsync(new AuditEntry(AuditCategory.Auth, "auth.login_shared_initial",
                "استخدام كلمة الدخول الأولية المشتركة", "Shared initial password used", "User", user.Id,
                ActorUserId: user.Id, ActorLogin: login, ActorName: user.FullNameAr), ct);
        await db.SaveChangesAsync(ct);
        return Result<AuthSession>.Success(session);
    }

    public async Task<Result<AuthSession>> ChangePasswordAsync(long userId, string newPassword,
        string confirmation, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmation))
            return Result<AuthSession>.Failure("PWD_TOO_SHORT");
        if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal))
            return Result<AuthSession>.Failure("PWD_CONFIRM_MISMATCH");
        User? user = await db.Users.Include(x => x.Employee).SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return Result<AuthSession>.Failure("AUTH_INVALID");
        if (user.AccountType != AccountType.Employee ||
            (user.FirstLoginCompletedAt is not null && !user.MustChangePassword &&
                !await RotationDueAsync(user, ct)))
            return Result<AuthSession>.Failure("FORBIDDEN");
        return await ChangePasswordCoreAsync(user, newPassword, ct);
    }

    public async Task<Result<AuthSession>> ChangeOwnPasswordAsync(long userId, string currentPassword,
        string newPassword, string confirmation, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(currentPassword))
            return Result<AuthSession>.Failure("AUTH_INVALID");
        if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmation))
            return Result<AuthSession>.Failure("PWD_TOO_SHORT");
        if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal))
            return Result<AuthSession>.Failure("PWD_CONFIRM_MISMATCH");
        User? user = await db.Users.Include(x => x.Employee).SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || user.AccountType != AccountType.Employee || user.FirstLoginCompletedAt is null)
            return Result<AuthSession>.Failure("FORBIDDEN");
        if (!hasher.Verify(currentPassword, user.PasswordHash))
            return Result<AuthSession>.Failure("AUTH_INVALID");
        return await ChangePasswordCoreAsync(user, newPassword, ct);
    }

    private async Task<Result<AuthSession>> ChangePasswordCoreAsync(User user, string newPassword, CancellationToken ct)
    {
        string initial = user.InitialPasswordEnc is null ? string.Empty : encryption.Decrypt(user.InitialPasswordEnc);
        string shared = await settings.GetAsync<string>("auth.shared_initial_password", ct);
        if (FixedEquals(newPassword, initial) || (!string.IsNullOrEmpty(shared) && FixedEquals(newPassword, shared)))
            return Result<AuthSession>.Failure("PWD_CONTAINS_CONTEXT");
        var policy = await db.PasswordPolicys.AsNoTracking().SingleAsync(ct);
        var blocked = await db.BlockedPasswords.AsNoTracking().Select(x => x.PasswordNormalized).ToListAsync(ct);
        var context = user.FullNameAr.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Concat([user.LoginName, initial, shared]);
        var errors = PasswordPolicyRules.Validate(newPassword, policy, blocked, context);
        if (errors.Count > 0) return Result<AuthSession>.Failure(errors[0]);
        user.PasswordHash = hasher.Hash(newPassword);
        user.PasswordEnc = encryption.Encrypt(newPassword);
        user.InitialPasswordEnc = null;
        user.MustChangePassword = false;
        user.FirstLoginCompletedAt ??= clock.UtcNow;
        user.PasswordChangedAt = clock.UtcNow;
        user.PasswordChangedBy = user.Id;
        user.SecurityStamp = Guid.NewGuid();
        var activeTokens = await db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null).ToListAsync(ct);
        foreach (var token in activeTokens)
        {
            token.RevokedAt = clock.UtcNow;
            token.RevokedReason = "PasswordChanged";
        }
        permissions.Invalidate(user.Id);
        userState.Invalidate(user.Id);
        AuthSession session = await IssueSessionAsync(user, false, false, null, null, ct);
        await audit.LogAsync(new AuditEntry(AuditCategory.Auth, "auth.password_changed",
            "تغيير كلمة المرور", "Password changed", "User", user.Id,
            ActorUserId: user.Id, ActorLogin: user.LoginName, ActorName: user.FullNameAr), ct);
        await db.SaveChangesAsync(ct);
        return Result<AuthSession>.Success(session);
    }

    public async Task<Result<AuthSession>> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        string hash = HashToken(refreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (token is null) return Result<AuthSession>.Failure("AUTH_INVALID");
        if (token.UsedAt.HasValue)
        {
            var family = await db.RefreshTokens.Where(x => x.FamilyId == token.FamilyId && x.RevokedAt == null).ToListAsync(ct);
            foreach (var member in family) { member.RevokedAt = clock.UtcNow; member.RevokedReason = "ReuseDetected"; }
            await audit.LogAsync(new AuditEntry(AuditCategory.Security, "security.refresh_reuse",
                "إعادة استخدام رمز تجديد الجلسة", "Refresh token reuse detected", "User", token.UserId,
                Success: false, ActorUserId: token.UserId), ct);
            await db.SaveChangesAsync(ct);
            return Result<AuthSession>.Failure("AUTH_INVALID");
        }
        if (token.RevokedAt.HasValue || token.ExpiresAt <= clock.UtcNow)
            return Result<AuthSession>.Failure("AUTH_INVALID");
        User? user = await db.Users.IgnoreQueryFilters().Include(x => x.Employee)
            .SingleOrDefaultAsync(x => x.Id == token.UserId, ct);
        if (user is null || !user.IsActive) return Result<AuthSession>.Failure("AUTH_INVALID");
        if (user.AccountType == AccountType.Employee && !user.AccessOverrideAllow)
        {
            string codes = await settings.GetAsync<string>("access.eligible_emp_status_codes", ct);
            if (user.Employee?.EmpStatus is not int status ||
                !codes.Split(',', StringSplitOptions.TrimEntries).Any(x => int.TryParse(x, out int code) && code == status))
                return Result<AuthSession>.Failure("AUTH_NOT_ELIGIBLE");
        }
        if (user.MustChangePassword || await RotationDueAsync(user, ct))
            return Result<AuthSession>.Failure("AUTH_PASSWORD_ROTATION_REQUIRED");
        if (user.AccountType == AccountType.Employee && user.FirstLoginCompletedAt is null)
            return Result<AuthSession>.Failure("AUTH_PASSWORD_CHANGE_REQUIRED");
        return await db.InTransactionAsync(async () =>
        {
            token.UsedAt = clock.UtcNow;
            AuthSession session = await IssueSessionAsync(user, token.RememberMe, false,
                token.FamilyId, token.ExpiresAt, ct);
            var replacement = db.RefreshTokens.Local.Last();
            await db.SaveChangesAsync(ct);
            token.ReplacedById = replacement.Id;
            await db.SaveChangesAsync(ct);
            return Result<AuthSession>.Success(session);
        }, ct);
    }

    public async Task<Result> LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(refreshToken))
        {
            string hash = HashToken(refreshToken);
            var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
            if (token is not null && token.RevokedAt is null)
            {
                token.RevokedAt = clock.UtcNow;
                token.RevokedReason = "Logout";
            }
        }
        await audit.LogAsync(new AuditEntry(AuditCategory.Auth, "auth.logout",
            "تسجيل خروج", "Logout"), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(string loginName, string email, string ip, CancellationToken ct)
    {
        // Always answer generically so the endpoint never reveals which part failed.
        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(email))
            return Result.Success();
        string login = LoginNameRules.NormalizeLogin(loginName);
        string normalizedEmail = email.Trim().ToLowerInvariant();
        string safeLogin = login.Length > 40 ? login[..40] : login;
        string safeEmail = normalizedEmail.Length > 254 ? normalizedEmail[..254] : normalizedEmail;
        string safeIp = ip.Length > 64 ? ip[..64] : ip;
        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset hourAgo = now.AddHours(-1);
        int accountLimit = await settings.GetAsync<int>("auth.forgot_max_per_account_hour", ct);
        int ipLimit = await settings.GetAsync<int>("auth.forgot_max_per_ip_hour", ct);
        bool accountExceeded = await db.ForgotPasswordAttempts.AsNoTracking()
            .CountAsync(x => x.LoginNameInput == safeLogin && x.RequestedAt >= hourAgo, ct) >= accountLimit;
        bool ipExceeded = await db.ForgotPasswordAttempts.AsNoTracking()
            .CountAsync(x => x.Ip == safeIp && x.RequestedAt >= hourAgo, ct) >= ipLimit;
        string outcome = "NoMatch";
        User? user = null;
        if (accountExceeded || ipExceeded) outcome = "RateLimited";
        else
        {
            user = await db.Users.IgnoreQueryFilters().Include(x => x.Employee)
                .SingleOrDefaultAsync(x => x.LoginName == login && x.AccountType == AccountType.Employee
                    && x.IsActive, ct);
            string? effectiveEmail = user?.EmailOverride ?? user?.Employee?.SourceEmail;
            if (user is null || string.IsNullOrWhiteSpace(effectiveEmail)) outcome = "NoEmail";
            else if (!string.Equals(effectiveEmail.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase))
                outcome = "NoMatch";
            else
            {
                bool blockShared = await settings.GetAsync<bool>("auth.forgot_block_shared_email", ct);
                int emailOwners = await db.Users.IgnoreQueryFilters().AsNoTracking()
                    .CountAsync(x => x.IsActive && x.AccountType == AccountType.Employee &&
                        (x.EmailOverride ?? x.Employee!.SourceEmail)!.ToLower() == normalizedEmail, ct);
                if (blockShared && emailOwners > 1)
                {
                    outcome = "SharedEmail";
                    string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)))
                        .ToLowerInvariant();
                    await db.InsertSharedEmailReviewIfAbsentAsync(key,
                        JsonSerializer.Serialize(new { email = normalizedEmail }), ct);
                }
                else
                {
                    // A changed password is stored only as a hash. Never email it back.
                    if (user.FirstLoginCompletedAt is not null || user.InitialPasswordEnc is null)
                        outcome = "ResetRequired";
                    else
                    {
                        string password = encryption.Decrypt(user.InitialPasswordEnc);
                        var (subject, body) = emailContent.ForgotPassword(user.FullNameAr,
                            password, user.PreferredLanguage);
                        db.EmailOutboxs.Add(new ZuLms.Domain.Entities.Notify.EmailOutbox
                        {
                            Kind = EmailKind.ForgotPassword,
                            ToAddress = normalizedEmail,
                            ToName = user.FullNameAr,
                            Subject = subject,
                            BodyHtml = body,
                            RelatedUserId = user.Id,
                            Status = OutboxStatus.Pending,
                            CreatedAt = now,
                            NextAttemptAt = now
                        });
                        outcome = "Sent";
                    }
                }
            }
        }
        db.ForgotPasswordAttempts.Add(new ForgotPasswordAttempt
        {
            LoginNameInput = safeLogin,
            EmailInput = safeEmail,
            Ip = safeIp,
            RequestedAt = now,
            Outcome = outcome,
            UserId = user?.Id
        });
        await audit.LogAsync(new AuditEntry(AuditCategory.Auth, "auth.forgot_password",
            "طلب استعادة كلمة المرور", "Password recovery requested", "User", user?.Id,
            Success: outcome == "Sent", FailureReason: outcome, ActorLogin: safeLogin), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetLanguageAsync(long userId, UiLanguage language,
        CancellationToken ct)
    {
        User? user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return Result.Failure("AUTH_INVALID");
        if (user.PreferredLanguage == language) return Result.Success();
        user.PreferredLanguage = language;
        user.UpdatedAt = clock.UtcNow;
        user.UpdatedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<AuthSession> IssueSessionAsync(User user, bool rememberMe, bool limited,
        Guid? familyId, DateTimeOffset? familyExpiresAt, CancellationToken ct)
    {
        DateTimeOffset now = clock.UtcNow;
        int accessMinutes = await settings.GetAsync<int>("auth.access_token_minutes", ct);
        if (limited)
        {
            AccountPermissionSet limitedPermissions = await permissions.GetAccountAsync(user.Id, ct);
            DateTimeOffset limitedExpiry = now.AddMinutes(accessMinutes);
            string limitedAccess = tokens.CreateAccessToken(user, limitedPermissions, true, limitedExpiry);
            return new AuthSession(limitedAccess, null, limitedExpiry, null, false, true, "/change-password");
        }
        int sessionHours = familyExpiresAt.HasValue ? 0 : await settings.GetAsync<int>("auth.session_hours", ct);
        int rememberDays = familyExpiresAt.HasValue ? 0 : await settings.GetAsync<int>("auth.remember_me_days", ct);
        var (accessExpiry, refreshExpiry) = AuthSessionRules.Expiries(now, accessMinutes,
            rememberMe, sessionHours, rememberDays, familyExpiresAt);
        AccountPermissionSet effective = await permissions.GetAccountAsync(user.Id, ct);
        string access = tokens.CreateAccessToken(user, effective, limited, accessExpiry);
        string refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refresh),
            FamilyId = familyId ?? Guid.NewGuid(),
            IssuedAt = now,
            ExpiresAt = refreshExpiry,
            RememberMe = rememberMe,
            Ip = string.Empty,
            UserAgent = string.Empty
        });
        return new AuthSession(access, refresh, accessExpiry, refreshExpiry, rememberMe,
            limited, limited ? "/change-password" : Landing(effective.Roles));
    }

    private async Task<bool> RotationDueAsync(User user, CancellationToken ct)
    {
        if (user.AccountType != AccountType.Employee || user.FirstLoginCompletedAt is null) return false;
        var policy = await db.PasswordPolicys.AsNoTracking().SingleAsync(ct);
        return PasswordRotationRules.IsDue(user.AccountType, user.FirstLoginCompletedAt,
            user.PasswordChangedAt, policy.RotationDays, clock.UtcNow);
    }

    private async Task AuditFailureAsync(User? user, string login, string code, CancellationToken ct)
    {
        await audit.LogAsync(new AuditEntry(AuditCategory.Auth, "auth.login_failed",
            "محاولة دخول غير ناجحة", "Failed login", "User", user?.Id,
            Success: false, FailureReason: code, ActorUserId: user?.Id,
            ActorLogin: login, ActorName: user?.FullNameAr), ct);
        await db.SaveChangesAsync(ct);
    }

    private static string Landing(IReadOnlySet<RoleCode> roles) =>
        roles.Contains(RoleCode.Admin) ? "/admin/dashboard" :
        roles.Contains(RoleCode.Quality) ? "/quality/dashboard" :
        roles.Contains(RoleCode.Dean) || roles.Contains(RoleCode.CenterManager) ? "/dean/dashboard" :
        roles.Contains(RoleCode.Instructor) ? "/instructor/courses" :
        roles.Contains(RoleCode.Member) ? "/member/dashboard" :
        roles.Contains(RoleCode.ExternalInstructor) ? "/instructor/courses" :
        "/access-denied";

    private static bool FixedEquals(string left, string right)
    {
        byte[] a = Encoding.UTF8.GetBytes(left.Normalize(NormalizationForm.FormC));
        byte[] b = Encoding.UTF8.GetBytes(right.Normalize(NormalizationForm.FormC));
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

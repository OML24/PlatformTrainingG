using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Credentials;

public sealed class CredentialService(IAppDbContext db, IClock clock,
    IPasswordHasher hasher, IEncryptionService encryption, ISettingsService settings,
    IPermissionService permissions, ICredentialReauthCache reauth, ICurrentUser current,
    IUserStateCache userState, IAuditLogger audit, IEmailContentBuilder emailContent) : ICredentialService
{
    public async Task<Result> ReauthenticateAsync(long actorUserId, string password, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "credentials.reveal", ct) ||
            current.UserId != actorUserId || string.IsNullOrEmpty(current.SessionId))
            return Result.Failure("FORBIDDEN");
        User? actor = await db.Users.SingleOrDefaultAsync(x => x.Id == actorUserId, ct);
        bool valid = actor is not null && hasher.Verify(password, actor.PasswordHash);
        await audit.LogAsync(new AuditEntry(AuditCategory.Security, "credentials.reauthenticate",
            "إعادة تحقق المسؤول", "Administrator reauthentication", "User", actorUserId,
            Success: valid, FailureReason: valid ? null : "AUTH_INVALID", ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        if (!valid) return Result.Failure("AUTH_INVALID");
        await reauth.GrantAsync(actorUserId, current.SessionId, clock.UtcNow.AddMinutes(5), ct);
        return Result.Success();
    }

    public async Task<Result<RevealedCredentialsDto>> RevealAsync(long actorUserId,
        long targetUserId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "credentials.reveal", ct))
            return Result<RevealedCredentialsDto>.Failure("FORBIDDEN");
        bool required = await settings.GetAsync<bool>("credentials.require_admin_reauth", ct);
        if (required && (current.UserId != actorUserId || string.IsNullOrEmpty(current.SessionId) ||
            !await reauth.IsGrantedAsync(actorUserId, current.SessionId, ct)))
            return Result<RevealedCredentialsDto>.Failure("REAUTH_REQUIRED");
        User? target = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == targetUserId, ct);
        if (target is null) return Result<RevealedCredentialsDto>.Failure("USER_NOT_FOUND");
        string? initial = target.FirstLoginCompletedAt is null && target.InitialPasswordEnc is not null
            ? encryption.Decrypt(target.InitialPasswordEnc) : null;
        string? currentPassword = string.IsNullOrEmpty(target.PasswordEnc)
            ? null : encryption.Decrypt(target.PasswordEnc);
        await audit.LogAsync(new AuditEntry(AuditCategory.Security, "credentials.reveal",
            "عرض كلمات مرور الحساب", "Account passwords revealed", "User", target.Id, target.FullNameAr,
            ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result<RevealedCredentialsDto>.Success(new(target.Id, initial, currentPassword));
    }

    public async Task<Result> ChangeAsync(long actorUserId, long targetUserId, uint expectedVersion,
        string newPassword, bool requireChangeAtNextLogin, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "credentials.change", ct))
            return Result.Failure("FORBIDDEN");
        User? target = await db.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == targetUserId, ct);
        if (target is null) return Result.Failure("USER_NOT_FOUND");
        if (target.Version != expectedVersion) return Result.Failure("CONFLICT");
        if (requireChangeAtNextLogin && target.AccountType != AccountType.Employee)
            return Result.Failure("PWD_CHANGE_NOT_ALLOWED");
        if (string.IsNullOrEmpty(newPassword)) return Result.Failure("PWD_TOO_SHORT");
        var policy = await db.PasswordPolicys.AsNoTracking().SingleAsync(ct);
        var blocked = await db.BlockedPasswords.AsNoTracking()
            .Select(x => x.PasswordNormalized).ToListAsync(ct);
        string initial = target.InitialPasswordEnc is null ? string.Empty : encryption.Decrypt(target.InitialPasswordEnc);
        string shared = await settings.GetAsync<string>("auth.shared_initial_password", ct);
        var context = target.FullNameAr.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Concat([target.LoginName, initial, shared]);
        var errors = PasswordPolicyRules.Validate(newPassword, policy, blocked, context);
        if (errors.Count > 0) return Result.Failure(errors[0]);
        target.PasswordHash = hasher.Hash(newPassword);
        target.PasswordEnc = encryption.Encrypt(newPassword);
        bool firstEmployeeLogin = target.AccountType == AccountType.Employee &&
            target.FirstLoginCompletedAt is null;
        target.InitialPasswordEnc = firstEmployeeLogin ? encryption.Encrypt(newPassword) : null;
        target.MustChangePassword = firstEmployeeLogin || requireChangeAtNextLogin;
        target.PasswordChangedAt = clock.UtcNow;
        target.PasswordChangedBy = actorUserId;
        target.SecurityStamp = Guid.NewGuid();
        var activeTokens = await db.RefreshTokens.Where(x => x.UserId == target.Id && x.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in activeTokens)
        {
            token.RevokedAt = clock.UtcNow;
            token.RevokedReason = "AdminPasswordChanged";
        }
        await audit.LogAsync(new AuditEntry(AuditCategory.Security, "credentials.change",
            "تغيير كلمة مرور حساب", "Account password changed by administrator", "User",
            target.Id, target.FullNameAr, ActorUserId: actorUserId), ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Failure("CONFLICT"); }
        permissions.Invalidate(target.Id);
        userState.Invalidate(target.Id);
        await reauth.InvalidateAsync(target.Id, ct);
        return Result.Success();
    }

    public async Task<Result> SetSharedInitialAsync(long actorUserId, uint expectedVersion,
        string password, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "credentials.set_shared_initial", ct))
            return Result.Failure("FORBIDDEN");
        var setting = await db.SystemSettings.SingleAsync(x =>
            x.SettingKey == "auth.shared_initial_password", ct);
        if (setting.Version != expectedVersion) return Result.Failure("CONFLICT");
        if (!string.IsNullOrEmpty(password))
        {
            var policy = await db.PasswordPolicys.AsNoTracking().SingleAsync(ct);
            var blocked = await db.BlockedPasswords.AsNoTracking()
                .Select(x => x.PasswordNormalized).ToListAsync(ct);
            var errors = PasswordPolicyRules.Validate(password, policy, blocked, []);
            if (errors.Count > 0) return Result.Failure(errors[0]);
        }
        setting.Value = string.IsNullOrEmpty(password) ? string.Empty : encryption.Encrypt(password);
        setting.UpdatedAt = clock.UtcNow;
        setting.UpdatedBy = actorUserId;
        await audit.LogAsync(new AuditEntry(AuditCategory.Settings, "credentials.set_shared_initial",
            "تعديل كلمة الدخول المشتركة", "Shared initial password changed", "SystemSetting",
            EntityLabel: setting.SettingKey, BeforeJson: JsonSerializer.Serialize(new { value = "[محذوف]" }),
            AfterJson: JsonSerializer.Serialize(new { value = "[محذوف]" }), ActorUserId: actorUserId), ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Failure("CONFLICT"); }
        settings.Invalidate("auth.shared_initial_password");
        return Result.Success();
    }

    public async Task<Result> QueueInitialAsync(long actorUserId, long targetUserId,
        bool confirmResend, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "credentials.send_initial", ct))
            return Result.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            User? user = await db.Users.IgnoreQueryFilters().Include(x => x.Employee)
                .SingleOrDefaultAsync(x => x.Id == targetUserId, ct);
            if (user is null) return Result.Failure("USER_NOT_FOUND");
            string? error = await CheckInitialEligibilityAsync(user, confirmResend, ct);
            if (error is not null) return Result.Failure(error);
            QueueInitial(user);
            await audit.LogAsync(new AuditEntry(AuditCategory.Security, "credentials.send_initial",
                "إرسال بيانات الدخول الأولية", "Initial credentials queued", "User", user.Id,
                user.FullNameAr, ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result<InitialCredentialsPreviewDto>> PreviewBulkInitialAsync(long actorUserId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "credentials.send_initial", ct))
            return Result<InitialCredentialsPreviewDto>.Failure("FORBIDDEN");
        var users = await db.Users.IgnoreQueryFilters().AsNoTracking().Include(x => x.Employee)
            .ToListAsync(ct);
        var pending = (await db.EmailOutboxs.AsNoTracking()
            .Where(x => x.Kind == EmailKind.InitialCredentials && x.Status == OutboxStatus.Pending &&
                x.RelatedUserId != null).Select(x => x.RelatedUserId!.Value).ToListAsync(ct)).ToHashSet();
        return Result<InitialCredentialsPreviewDto>.Success(Summarize(users, pending));
    }

    public async Task<Result<InitialCredentialsPreviewDto>> QueueBulkInitialAsync(long actorUserId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "credentials.send_initial", ct))
            return Result<InitialCredentialsPreviewDto>.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var users = await db.Users.IgnoreQueryFilters().Include(x => x.Employee).ToListAsync(ct);
            var pending = (await db.EmailOutboxs.AsNoTracking()
                .Where(x => x.Kind == EmailKind.InitialCredentials && x.Status == OutboxStatus.Pending &&
                    x.RelatedUserId != null).Select(x => x.RelatedUserId!.Value).ToListAsync(ct)).ToHashSet();
            var preview = Summarize(users, pending);
            foreach (var user in users.Where(x => IsEligible(x) &&
                (x.CredentialsEmailStatus is CredentialsEmailStatus.NotSent or CredentialsEmailStatus.Failed) &&
                !pending.Contains(x.Id))) QueueInitial(user);
            await audit.LogAsync(new AuditEntry(AuditCategory.Security, "credentials.send_initial_bulk",
                "إرسال بيانات الدخول جماعياً", "Bulk initial credentials queued", "EmailOutbox",
                Success: true, AfterJson: JsonSerializer.Serialize(new { queued = preview.Eligible }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result<InitialCredentialsPreviewDto>.Success(preview);
        }, ct);
    }

    public async Task<Result> ResolveUncertainAsync(long actorUserId, long targetUserId,
        bool received, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "credentials.send_initial", ct))
            return Result.Failure("FORBIDDEN");
        User? user = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == targetUserId, ct);
        if (user is null) return Result.Failure("USER_NOT_FOUND");
        if (user.CredentialsEmailStatus != CredentialsEmailStatus.Uncertain)
            return Result.Failure("NOT_UNCERTAIN");
        user.CredentialsEmailStatus = received ? CredentialsEmailStatus.Sent : CredentialsEmailStatus.Failed;
        user.CredentialsEmailSentAt = received ? clock.UtcNow : null;
        user.CredentialsEmailLastError = received ? null : "ManuallyMarkedFailed";
        await audit.LogAsync(new AuditEntry(AuditCategory.Security, "credentials.resolve_uncertain",
            "معالجة نتيجة إرسال غير مؤكدة", "Uncertain delivery resolved", "User", user.Id,
            user.FullNameAr, AfterJson: JsonSerializer.Serialize(new { received }),
            ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<string?> CheckInitialEligibilityAsync(User user, bool confirmResend, CancellationToken ct)
    {
        if (!IsEligible(user)) return "INITIAL_NOT_ELIGIBLE";
        if (user.CredentialsEmailStatus == CredentialsEmailStatus.Uncertain) return "UNCERTAIN_DELIVERY";
        if (user.CredentialsEmailStatus == CredentialsEmailStatus.Sent && !confirmResend)
            return "RESEND_CONFIRMATION_REQUIRED";
        bool pending = await db.EmailOutboxs.AnyAsync(x => x.Kind == EmailKind.InitialCredentials &&
            x.RelatedUserId == user.Id && x.Status == OutboxStatus.Pending, ct);
        return pending ? "ALREADY_PENDING" : null;
    }

    private static bool IsEligible(User user) => user.AccountType == AccountType.Employee &&
        user.IsActive && user.FirstLoginCompletedAt is null &&
        !string.IsNullOrWhiteSpace(user.EmailOverride ?? user.Employee?.SourceEmail) &&
        user.InitialPasswordEnc is not null;

    private void QueueInitial(User user)
    {
        string email = (user.EmailOverride ?? user.Employee!.SourceEmail)!.Trim().ToLowerInvariant();
        string password = encryption.Decrypt(user.InitialPasswordEnc!);
        var (subject, body) = emailContent.InitialCredentials(user.FullNameAr, user.LoginName,
            password, user.PreferredLanguage);
        db.EmailOutboxs.Add(new EmailOutbox
        {
            Kind = EmailKind.InitialCredentials, ToAddress = email, ToName = user.FullNameAr,
            Subject = subject, BodyHtml = body, RelatedUserId = user.Id,
            Status = OutboxStatus.Pending, CreatedAt = clock.UtcNow, NextAttemptAt = clock.UtcNow
        });
        user.CredentialsEmailLastError = null;
    }

    private static InitialCredentialsPreviewDto Summarize(IEnumerable<User> users, HashSet<long> pending)
    {
        int eligible = 0, sent = 0, uncertain = 0, noEmail = 0, waiting = 0, ineligible = 0;
        foreach (var user in users)
        {
            if (user.AccountType != AccountType.Employee || !user.IsActive ||
                user.FirstLoginCompletedAt is not null || user.InitialPasswordEnc is null) ineligible++;
            else if (string.IsNullOrWhiteSpace(user.EmailOverride ?? user.Employee?.SourceEmail)) noEmail++;
            else if (pending.Contains(user.Id)) waiting++;
            else if (user.CredentialsEmailStatus == CredentialsEmailStatus.Sent) sent++;
            else if (user.CredentialsEmailStatus == CredentialsEmailStatus.Uncertain) uncertain++;
            else eligible++;
        }
        return new(eligible, sent, uncertain, noEmail, waiting, ineligible);
    }
}

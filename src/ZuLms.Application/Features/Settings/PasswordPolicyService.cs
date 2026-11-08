using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Settings;

public sealed class PasswordPolicyService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings, IAuditLogger audit)
    : IPasswordPolicyService
{
    public async Task<Result<PasswordPolicyDto>> GetAsync(long actorUserId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.password_policy", ct))
            return Result<PasswordPolicyDto>.Failure("FORBIDDEN");
        var policy = await db.PasswordPolicys.AsNoTracking().SingleAsync(ct);
        return Result<PasswordPolicyDto>.Success(ToDto(policy));
    }

    public async Task<Result> UpdateAsync(long actorUserId, PasswordPolicyDto value, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "password_policy.edit", ct))
            return Result.Failure("FORBIDDEN");
        if (value.MinLength < 1 || value.MaxLength < value.MinLength ||
            value.RotationDays is <= 0) return Result.Failure("POLICY_INVALID");
        var policy = await db.PasswordPolicys.SingleAsync(ct);
        var before = ToDto(policy);
        policy.MinLength = value.MinLength;
        policy.MaxLength = value.MaxLength;
        policy.AllowSpaces = value.AllowSpaces;
        policy.AllowArabic = value.AllowArabic;
        policy.BlockCommon = value.BlockCommon;
        policy.BlockContext = value.BlockContext;
        policy.RequireUpper = value.RequireUpper;
        policy.RequireLower = value.RequireLower;
        policy.RequireDigit = value.RequireDigit;
        policy.RequireSymbol = value.RequireSymbol;
        policy.RotationDays = value.RotationDays;
        policy.UpdatedAt = clock.UtcNow;
        policy.UpdatedBy = actorUserId;
        await audit.LogAsync(new AuditEntry(AuditCategory.Settings, "password_policy.edit",
            "تعديل سياسة كلمات المرور", "Password policy changed", "PasswordPolicy", 1,
            BeforeJson: JsonSerializer.Serialize(before), AfterJson: JsonSerializer.Serialize(value),
            ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<BlockedPasswordPageDto>> ListBlockedAsync(long actorUserId,
        string? search, int page, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.password_policy", ct))
            return Result<BlockedPasswordPageDto>.Failure("FORBIDDEN");
        if (page < 1) return Result<BlockedPasswordPageDto>.Failure("QUERY_INVALID");
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (page > int.MaxValue / pageSize)
            return Result<BlockedPasswordPageDto>.Failure("QUERY_INVALID");
        var query = db.BlockedPasswords.AsNoTracking().AsQueryable();
        string term = Normalize(search);
        if (term.Length > 0) query = query.Where(x => x.PasswordNormalized.Contains(term));
        int total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.PasswordNormalized).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new BlockedPasswordDto(x.Id, x.PasswordNormalized, x.Source)).ToListAsync(ct);
        return Result<BlockedPasswordPageDto>.Success(new(page, pageSize, total, rows));
    }

    public async Task<Result> AddBlockedAsync(long actorUserId, string password, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "password_policy.edit", ct))
            return Result.Failure("FORBIDDEN");
        string normalized = Normalize(password);
        if (normalized.Length is < 1 or > 200) return Result.Failure("BLOCKED_PASSWORD_INVALID");
        if (await db.BlockedPasswords.AnyAsync(x => x.PasswordNormalized == normalized, ct))
            return Result.Failure("BLOCKED_PASSWORD_EXISTS");
        db.BlockedPasswords.Add(new BlockedPassword
        {
            PasswordNormalized = normalized, Source = "Admin", CreatedBy = actorUserId
        });
        await audit.LogAsync(new AuditEntry(AuditCategory.Security, "password_policy.block_add",
            "إضافة كلمة محظورة", "Blocked password added", "BlockedPassword",
            ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RemoveBlockedAsync(long actorUserId, long id, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "password_policy.edit", ct))
            return Result.Failure("FORBIDDEN");
        BlockedPassword? row = await db.BlockedPasswords.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return Result.Failure("BLOCKED_PASSWORD_NOT_FOUND");
        db.BlockedPasswords.Remove(row);
        await audit.LogAsync(new AuditEntry(AuditCategory.Security, "password_policy.block_remove",
            "إزالة كلمة محظورة", "Blocked password removed", "BlockedPassword", id,
            ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static string Normalize(string? text) =>
        (text ?? string.Empty).Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();

    private static PasswordPolicyDto ToDto(PasswordPolicy value) => new(value.MinLength,
        value.MaxLength, value.AllowSpaces, value.AllowArabic, value.BlockCommon,
        value.BlockContext, value.RequireUpper, value.RequireLower, value.RequireDigit,
        value.RequireSymbol, value.RotationDays);
}

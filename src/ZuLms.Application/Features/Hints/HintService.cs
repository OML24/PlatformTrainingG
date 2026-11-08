using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Common;
using ZuLms.Domain.Entities.Config;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Hints;

public sealed class HintService(
    IAppDbContext db, IClock clock, IPermissionService permissions,
    ISettingsService settings, IAuditLogger audit) : IHintService
{
    public async Task<Result<IReadOnlyList<HintDto>>> ListByContextAsync(string? context,
        CancellationToken ct)
    {
        var query = db.FieldHints.AsNoTracking().Where(x => x.IsEnabled);
        if (!string.IsNullOrWhiteSpace(context))
        {
            string key = context.Trim();
            query = query.Where(x => x.ContextKey == key);
        }
        var rows = await query.OrderBy(x => x.FieldKey)
            .Select(x => new HintDto(x.FieldKey, x.HintAr, x.HintEn))
            .ToListAsync(ct);
        return Result<IReadOnlyList<HintDto>>.Success(rows);
    }

    public async Task<Result<HintPageDto>> ListAllAsync(long actorUserId, string? search, int page,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "hints.manage", ct))
            return Result<HintPageDto>.Failure("FORBIDDEN");
        if (page < 1) return Result<HintPageDto>.Failure("QUERY_INVALID");
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (page > int.MaxValue / pageSize) return Result<HintPageDto>.Failure("QUERY_INVALID");

        var query = db.FieldHints.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(x => x.FieldKey.Contains(term) || x.ContextKey.Contains(term));
        }
        int total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.ContextKey).ThenBy(x => x.FieldKey)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new HintAdminDto(x.Id, x.FieldKey, x.ContextKey, x.HintAr, x.HintEn,
                x.IsEnabled, x.Version))
            .ToListAsync(ct);
        return Result<HintPageDto>.Success(new HintPageDto(page, pageSize, total, rows));
    }

    public async Task<Result<long>> CreateAsync(long actorUserId, SaveHintInput input, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "hints.manage", ct))
            return Result<long>.Failure("FORBIDDEN");
        string? invalid = Validate(input);
        if (invalid is not null) return Result<long>.Failure(invalid);
        if (await db.FieldHints.AnyAsync(x => x.FieldKey == input.FieldKey.Trim()
                && x.ContextKey == input.ContextKey.Trim(), ct))
            return Result<long>.Failure("HINT_EXISTS");

        var hint = new FieldHint
        {
            FieldKey = input.FieldKey.Trim(), ContextKey = input.ContextKey.Trim(),
            HintAr = input.HintAr.Trim(), HintEn = Trimmed(input.HintEn),
            IsEnabled = input.IsEnabled, IsActive = true,
            CreatedAt = clock.UtcNow, CreatedByUserId = actorUserId
        };
        db.FieldHints.Add(hint);
        await audit.LogAsync(new AuditEntry(AuditCategory.Settings, "hint.create",
            "إضافة تلميح حقل", "Field hint created", "FieldHint", EntityLabel: hint.FieldKey,
            ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result<long>.Success(hint.Id);
    }

    public async Task<Result> UpdateAsync(long actorUserId, long id, SaveHintInput input,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "hints.manage", ct))
            return Result.Failure("FORBIDDEN");
        string? invalid = Validate(input);
        if (invalid is not null) return Result.Failure(invalid);
        FieldHint? hint = await db.FieldHints.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (hint is null) return Result.Failure("HINT_NOT_FOUND");
        bool duplicate = await db.FieldHints.AnyAsync(x => x.Id != id
            && x.FieldKey == input.FieldKey.Trim() && x.ContextKey == input.ContextKey.Trim(), ct);
        if (duplicate) return Result.Failure("HINT_EXISTS");

        hint.FieldKey = input.FieldKey.Trim();
        hint.ContextKey = input.ContextKey.Trim();
        hint.HintAr = input.HintAr.Trim();
        hint.HintEn = Trimmed(input.HintEn);
        hint.IsEnabled = input.IsEnabled;
        hint.UpdatedAt = clock.UtcNow;
        hint.UpdatedByUserId = actorUserId;
        await audit.LogAsync(new AuditEntry(AuditCategory.Settings, "hint.update",
            "تعديل تلميح حقل", "Field hint updated", "FieldHint", id, hint.FieldKey,
            ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(long actorUserId, long id, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "hints.manage", ct))
            return Result.Failure("FORBIDDEN");
        FieldHint? hint = await db.FieldHints.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (hint is null) return Result.Failure("HINT_NOT_FOUND");
        hint.Deactivate(actorUserId, "admin", clock.UtcNow);
        await audit.LogAsync(new AuditEntry(AuditCategory.Settings, "hint.deactivate",
            "تعطيل تلميح حقل", "Field hint deactivated", "FieldHint", id, hint.FieldKey,
            ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static string? Validate(SaveHintInput input)
    {
        if (string.IsNullOrWhiteSpace(input.FieldKey) || input.FieldKey.Trim().Length > 120)
            return "HINT_FIELD_KEY_INVALID";
        if (string.IsNullOrWhiteSpace(input.ContextKey) || input.ContextKey.Trim().Length > 80)
            return "HINT_CONTEXT_KEY_INVALID";
        if (string.IsNullOrWhiteSpace(input.HintAr) || input.HintAr.Trim().Length > 1000)
            return "HINT_TEXT_INVALID";
        if (input.HintEn is not null && input.HintEn.Trim().Length > 1000)
            return "HINT_TEXT_INVALID";
        return null;
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

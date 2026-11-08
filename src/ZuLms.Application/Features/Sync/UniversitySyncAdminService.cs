using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Users;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Sync;

public sealed class UniversitySyncAdminService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings, IUserService users,
    IAuditLogger audit) : IUniversitySyncAdminService
{
    public async Task<Result<IReadOnlyList<UniversitySideDto>>> ListSidesAsync(long actorUserId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.sync", ct))
            return Result<IReadOnlyList<UniversitySideDto>>.Failure("FORBIDDEN");
        var rows = await db.UniversitySides.AsNoTracking().OrderBy(x => x.SideCode)
            .Select(x => new UniversitySideDto(x.SideCode, x.NameAr, x.IsImported, x.LastSeenAt))
            .ToListAsync(ct);
        return Result<IReadOnlyList<UniversitySideDto>>.Success(rows);
    }

    public async Task<Result> SetSideImportedAsync(long actorUserId, int sideCode,
        bool imported, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "sync.run", ct))
            return Result.Failure("FORBIDDEN");
        var side = await db.UniversitySides.SingleOrDefaultAsync(x => x.SideCode == sideCode, ct);
        if (side is null) return Result.Failure("SIDE_NOT_FOUND");
        if (side.IsImported == imported) return Result.Success();
        side.IsImported = imported;
        await audit.LogAsync(new AuditEntry(AuditCategory.Sync, "sync.side_imported",
            "تعديل استيراد جهة", "University side import selection changed",
            "UniversitySide", sideCode, side.NameAr, ActorUserId: actorUserId), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<SyncRunDto>>> ListRunsAsync(long actorUserId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.sync", ct))
            return Result<IReadOnlyList<SyncRunDto>>.Failure("FORBIDDEN");
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        var rows = await db.SyncRuns.AsNoTracking().OrderByDescending(x => x.Id)
            .Take(pageSize)
            .Select(x => new SyncRunDto(x.Id, x.StartedAt, x.FinishedAt,
                x.Status, x.StatsJson, x.ErrorText, x.TriggeredBy)).ToListAsync(ct);
        return Result<IReadOnlyList<SyncRunDto>>.Success(rows);
    }

    public async Task<Result<SyncReviewPageDto>> ListReviewsAsync(long actorUserId,
        SyncReviewReason? reason, int page, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.sync", ct))
            return Result<SyncReviewPageDto>.Failure("FORBIDDEN");
        if (page < 1 || reason.HasValue && !Enum.IsDefined(reason.Value))
            return Result<SyncReviewPageDto>.Failure("QUERY_INVALID");
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (page > int.MaxValue / pageSize) return Result<SyncReviewPageDto>.Failure("QUERY_INVALID");
        var query = db.SyncReviewItems.AsNoTracking().Where(x => x.ResolvedAt == null);
        if (reason.HasValue) query = query.Where(x => x.Reason == reason.Value);
        int total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize)
            .Take(pageSize).Select(x => new SyncReviewDto(x.Id, x.EntityType, x.EntityKey,
                x.Reason, x.Details, x.ResolvedAt, x.Resolution)).ToListAsync(ct);
        return Result<SyncReviewPageDto>.Success(new(page, pageSize, total, rows));
    }

    public async Task<Result> ResolveReviewAsync(long actorUserId, long reviewId,
        SyncReviewResolution resolution, string? notes, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "sync.review.resolve", ct))
            return Result.Failure("FORBIDDEN");
        if (!Enum.IsDefined(resolution)) return Result.Failure("RESOLUTION_INVALID");
        if (resolution == SyncReviewResolution.Disable && string.IsNullOrWhiteSpace(notes))
            return Result.Failure("REASON_REQUIRED");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var item = await db.SyncReviewItems.SingleOrDefaultAsync(x => x.Id == reviewId, ct);
            if (item is null) return Result.Failure("REVIEW_NOT_FOUND");
            if (item.ResolvedAt is not null) return Result.Failure("REVIEW_ALREADY_RESOLVED");
            if (resolution == SyncReviewResolution.Disable)
            {
                if (item.EntityType != "Employee") return Result.Failure("REVIEW_DISABLE_NOT_ALLOWED");
                string[] parts = item.EntityKey.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int side) ||
                    !int.TryParse(parts[1], out int code)) return Result.Failure("REVIEW_KEY_INVALID");
                long? userId = await db.Users.IgnoreQueryFilters().Where(x => x.Employee != null &&
                    x.Employee.SideCode == side && x.Employee.EmployeeCode == code)
                    .Select(x => (long?)x.Id).SingleOrDefaultAsync(ct);
                if (userId is null) return Result.Failure("REVIEW_USER_NOT_FOUND");
                var user = await db.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == userId, ct);
                if (user.IsActive)
                {
                    var disabled = await users.DeactivateAsync(actorUserId, user.Id,
                        user.Version, notes!, ct);
                    if (!disabled.Ok) return disabled;
                }
            }
            item.ResolvedAt = clock.UtcNow;
            item.ResolvedBy = actorUserId;
            item.Resolution = resolution;
            item.Notes = notes?.Trim();
            await audit.LogAsync(new AuditEntry(AuditCategory.Sync, "sync.review.resolve",
                "معالجة عنصر مراجعة", "Sync review item resolved", "SyncReviewItem", item.Id,
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }
}

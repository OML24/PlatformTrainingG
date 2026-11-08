using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Tracks;

public interface ITrackAdminService
{
    Task<Result<IReadOnlyList<TrackAdminDto>>> ListAsync(long actorUserId,
        bool includeArchived, CancellationToken ct);
    Task<Result> SaveAsync(long actorUserId, long? trackId, string nameAr,
        string? nameEn, int sortOrder, CancellationToken ct);
    Task<Result> SetArchivedAsync(long actorUserId, long trackId, bool archived,
        CancellationToken ct);
}

public sealed record TrackAdminDto(long Id, string NameAr, string? NameEn,
    int SortOrder, bool IsArchived);

public sealed class TrackAdminService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IAuditLogger audit) : ITrackAdminService
{
    public async Task<Result<IReadOnlyList<TrackAdminDto>>> ListAsync(long actorUserId,
        bool includeArchived, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "tracks.manage", ct))
            return Result<IReadOnlyList<TrackAdminDto>>.Failure("FORBIDDEN");
        var query = db.TrainingTracks.AsNoTracking();
        if (!includeArchived) query = query.Where(x => !x.IsArchived);
        var rows = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new TrackAdminDto(x.Id, x.NameAr, x.NameEn, x.SortOrder, x.IsArchived))
            .ToListAsync(ct);
        return Result<IReadOnlyList<TrackAdminDto>>.Success(rows);
    }

    public async Task<Result> SaveAsync(long actorUserId, long? trackId, string nameAr,
        string? nameEn, int sortOrder, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "tracks.manage", ct))
            return Result.Failure("FORBIDDEN");
        if (string.IsNullOrWhiteSpace(nameAr) || nameAr.Trim().Length > 200 ||
            ArabicSearchNormalizer.Normalize(nameAr).Length == 0)
            return Result.Failure("TRACK_INVALID");
        DateTimeOffset now = clock.UtcNow;
        return await db.InTransactionAsync(async () =>
        {
            if (trackId is long id)
            {
                var track = await db.TrainingTracks.SingleOrDefaultAsync(x => x.Id == id, ct);
                if (track is null) return Result.Failure("TRACK_NOT_FOUND");
                track.NameAr = nameAr.Trim(); track.NameEn = Clean(nameEn);
                track.SortOrder = sortOrder;
                track.UpdatedAt = now; track.UpdatedByUserId = actorUserId;
            }
            else
            {
                db.TrainingTracks.Add(new TrainingTrack
                {
                    NameAr = nameAr.Trim(), NameEn = Clean(nameEn),
                    SortOrder = sortOrder, IsActive = true,
                    CreatedAt = now, CreatedByUserId = actorUserId
                });
            }
            await audit.LogAsync(new AuditEntry(AuditCategory.Settings,
                trackId is null ? "track.create" : "track.edit",
                trackId is null ? "إضافة محور تدريبي" : "تعديل محور تدريبي",
                trackId is null ? "Training track created" : "Training track edited",
                "TrainingTrack", trackId ?? 0, nameAr.Trim(),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> SetArchivedAsync(long actorUserId, long trackId,
        bool archived, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "tracks.manage", ct))
            return Result.Failure("FORBIDDEN");
        DateTimeOffset now = clock.UtcNow;
        return await db.InTransactionAsync(async () =>
        {
            var track = await db.TrainingTracks.SingleOrDefaultAsync(x => x.Id == trackId, ct);
            if (track is null) return Result.Failure("TRACK_NOT_FOUND");
            if (track.IsArchived == archived)
                return Result.Failure(archived ? "TRACK_ALREADY_ARCHIVED" : "TRACK_NOT_ARCHIVED");
            track.IsArchived = archived;
            track.ArchivedAt = archived ? now : null;
            track.ArchivedByUserId = archived ? actorUserId : null;
            track.UpdatedAt = now; track.UpdatedByUserId = actorUserId;
            await audit.LogAsync(new AuditEntry(AuditCategory.Settings,
                archived ? "track.archive" : "track.unarchive",
                archived ? "أرشفة محور تدريبي" : "إلغاء أرشفة محور تدريبي",
                archived ? "Training track archived" : "Training track unarchived",
                "TrainingTrack", trackId, track.NameAr, ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Sync;

public interface IUniversitySyncAdminService
{
    Task<Result<IReadOnlyList<UniversitySideDto>>> ListSidesAsync(long actorUserId,
        CancellationToken cancellationToken);
    Task<Result> SetSideImportedAsync(long actorUserId, int sideCode, bool imported,
        CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<SyncRunDto>>> ListRunsAsync(long actorUserId,
        CancellationToken cancellationToken);
    Task<Result<SyncReviewPageDto>> ListReviewsAsync(long actorUserId, SyncReviewReason? reason,
        int page, CancellationToken cancellationToken);
    Task<Result> ResolveReviewAsync(long actorUserId, long reviewId,
        SyncReviewResolution resolution, string? notes, CancellationToken cancellationToken);
}

public sealed record UniversitySideDto(int SideCode, string NameAr, bool IsImported,
    DateTimeOffset? LastSeenAt);
public sealed record SyncReviewDto(long Id, string EntityType, string EntityKey,
    SyncReviewReason Reason, string? Details, DateTimeOffset? ResolvedAt,
    SyncReviewResolution? Resolution);
public sealed record SyncReviewPageDto(int Page, int PageSize, int Total,
    IReadOnlyList<SyncReviewDto> Items);

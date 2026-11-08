using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Courses;

public interface ICourseCancellationService
{
    Task<Result<long>> RequestAsync(long actorUserId, long courseId,
        string reason, long? letterFileId, CancellationToken ct);
    Task<Result<IReadOnlyList<CancellationRequestDto>>> PendingAsync(
        long actorUserId, CancellationToken ct);
    Task<Result> DecideAsync(long actorUserId, long requestId,
        bool approve, string? notes, CancellationToken ct);
    Task<Result> CancelDirectAsync(long actorUserId, long courseId,
        string reason, long letterFileId, CancellationToken ct);
}

public sealed record CancellationRequestDto(long Id, long CourseId,
    string CourseNo, string TitleAr, string Reason, long? LetterFileId,
    DateTimeOffset RequestedAt, string RequestedByNameAr);

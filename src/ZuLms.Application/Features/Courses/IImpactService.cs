using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface IImpactService
{
    Task<Result<IReadOnlyList<ImpactDto>>> MineAsync(long actorId,
        long courseId, CancellationToken ct);
    Task<Result<IReadOnlyList<ImpactDto>>> PendingAsync(long actorId,
        long courseId, CancellationToken ct);
    Task<Result<IReadOnlyList<ImpactReviewCourseDto>>> ReviewCoursesAsync(
        long actorId, CancellationToken ct);
    Task<Result<long>> SubmitAsync(long actorId, long courseId,
        ImpactType type, long fileId, CancellationToken ct);
    Task<Result> ReviewAsync(long actorId, long courseId, long submissionId,
        bool approve, string? notes, CancellationToken ct);
}

public sealed record ImpactDto(long Id, long ParticipantId, string NameAr,
    ImpactType Type, ImpactStatus Status, int SequenceNo,
    long FileId, string? Notes);
public sealed record ImpactReviewCourseDto(long Id, string CourseNo,
    string TitleAr, string Destination, int PendingCount);

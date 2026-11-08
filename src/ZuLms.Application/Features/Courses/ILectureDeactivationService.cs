using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Courses;

public interface ILectureDeactivationService
{
    Task<Result> DeactivateAsync(long actorId, long lectureId,
        string? reason, CancellationToken ct);
}

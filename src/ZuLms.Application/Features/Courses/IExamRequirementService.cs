using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Courses;

public interface IExamRequirementService
{
    Task<Result> ChangeAsync(long actorId, long courseId, bool hasExam,
        string? disableReason, CancellationToken ct);
}

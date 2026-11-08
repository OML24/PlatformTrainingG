using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Courses;

public interface ICourseFinishService
{
    Task<Result<int>> FinishAsync(long actorId, long courseId,
        CancellationToken ct);
}

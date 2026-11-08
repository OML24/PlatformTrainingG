using ZuLms.Application.Common.Results;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public interface IEligibilitySnapshotService
{
    Task<Result<EligibilityResult>> EvaluateAsync(long courseId,
        long participantId, bool treatCourseAsActive, CancellationToken ct);
}

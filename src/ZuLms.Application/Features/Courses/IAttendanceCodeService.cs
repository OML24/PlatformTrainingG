using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Courses;

public interface IAttendanceCodeService
{
    Task<Result> EnterAsync(long actorId, long lectureId,
        string code, CancellationToken ct);
}

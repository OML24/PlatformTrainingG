using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Courses;

public interface ILectureMaterialService
{
    Task<Result<IReadOnlyList<LectureMaterialDto>>> ListAsync(long actorId,
        long lectureId, CancellationToken ct);
    Task<Result<long>> AttachAsync(long actorId, long lectureId,
        long fileId, string? title, CancellationToken ct);
}

public sealed record LectureMaterialDto(long Id, long FileId,
    string Name, string? Title);

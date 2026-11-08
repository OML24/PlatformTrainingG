using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Courses;

public interface IAdminCourseLifecycleService
{
    Task<Result<CourseDeactivationPreviewDto>> PreviewDeactivationAsync(
        long actorUserId, long courseId, CancellationToken ct);
    Task<Result> DeactivateAsync(long actorUserId, long courseId,
        uint expectedVersion, string reason,
        CertificateDeactivationAction? certificateAction, CancellationToken ct);
    Task<Result> RestoreAsync(long actorUserId, long courseId,
        uint expectedVersion, CancellationToken ct);
    Task<Result> ArchiveAsync(long actorUserId, long courseId,
        uint expectedVersion, bool archive, CancellationToken ct);
    Task<Result> ReopenAsync(long actorUserId, long courseId,
        uint expectedVersion, bool confirmed, CancellationToken ct);
}

public enum CertificateDeactivationAction { Keep, Hide }
public sealed record CourseDeactivationPreviewDto(long CourseId,
    uint Version, int IssuedCertificates);

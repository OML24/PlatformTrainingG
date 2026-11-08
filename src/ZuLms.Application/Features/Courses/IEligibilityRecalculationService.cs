using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface IEligibilityRecalculationService
{
    Task<Result<EligibilityRecalculationDto>> RecalculateParticipantAsync(
        long courseId, long participantId, CertificateRevokeReason? revokeReason,
        long? actorId, CancellationToken ct);
    Task<Result> RecalculateCourseAsync(long courseId,
        CertificateRevokeReason? revokeReason, long? actorId,
        CancellationToken ct);
}

public sealed record EligibilityRecalculationDto(bool Eligible,
    bool CertificatePending, string? PendingReason, int RevokedCertificates);

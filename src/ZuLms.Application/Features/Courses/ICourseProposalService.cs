using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface ICourseProposalService
{
    Task<Result<long>> ProposeAsync(long deanUserId, long centerId,
        CourseProposalInput input, CancellationToken ct);
    Task<Result> ResubmitAsync(long deanUserId, long courseId, uint expectedVersion,
        CourseProposalInput input, CancellationToken ct);
    Task<Result> DecideAsync(long actorUserId, long courseId, uint expectedVersion,
        CourseProposalDecisionInput input, CancellationToken ct);
}

public sealed record CourseProposalInput(
    string TitleAr, string? TitleEn, long TrackId, DeliveryMode DeliveryMode,
    string ObjectivesAr, string? ObjectivesEn, string TargetAudienceAr,
    string? TargetAudienceEn, decimal TotalHours, DateOnly PlannedStartDate,
    DateOnly PlannedEndDate, AudienceType AudienceType,
    DateTimeOffset? RegistrationDeadline, int? MaxParticipants, bool HasExam,
    long? InternalInstructorUserId, string? ExternalNameAr,
    string? ExternalNameEn, string? ExternalOrgAr, string? ExternalOrgEn,
    string? ExternalEmail, long? CouncilLetterFileId, long? InstructorCvFileId);

public enum ExternalInstructorDecision
{
    ExistingAccount,
    RequestNewAccount,
    ManagedByQuality
}

public sealed record CourseProposalDecisionInput(
    ProposalDecision Decision, string? Notes, DateOnly? EvaluationEndDate,
    AudienceType? AudienceType, long? TargetCenterId,
    DateTimeOffset? RegistrationDeadline, int? MaxParticipants, bool? HasExam,
    ExternalInstructorDecision? ExternalDecision, long? ExternalInstructorUserId);

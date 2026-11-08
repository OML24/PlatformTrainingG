namespace ZuLms.Domain.Enums;

public enum NotificationType
{
    ProposalSubmitted, ProposalDecision, CourseApproved, ParticipantAdded, ParticipantRemoved,
    LectureCreated, CourseFinished, CertificateIssued, CertificateRevoked, EvaluationReopened,
    ImpactSubmitted, ImpactDecision, CancellationRequested, CancellationDecision,
    CourseCancelled, CourseDisabled, CourseRestored, CourseArchived,
    CourseUnarchived, NeedSubmitted, NeedDecision,
    ExtInstructorRequested, ExtInstructorReady, ProCertSubmitted, ProCertApproved,
    ProCertRejected, SyncReviewItems
}

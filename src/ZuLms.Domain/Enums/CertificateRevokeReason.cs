namespace ZuLms.Domain.Enums;

public enum CertificateRevokeReason
{
    AttendanceCorrection,
    ExamResultChanged,
    ExamRequirementChanged,
    CourseCancelled,
    CourseReopened,
    ParticipantRemoved,
    CourseDisabledRestoreIneligible,
    ManualByAdmin
}


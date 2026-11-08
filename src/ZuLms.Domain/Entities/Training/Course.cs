using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class Course : BaseEntity, ISoftDeletable, IArchivable
{
    public string CourseNo { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public long TrackId { get; set; }
    public DeliveryMode DeliveryMode { get; set; }
    public string ObjectivesAr { get; set; } = string.Empty;
    public string? ObjectivesEn { get; set; }
    public string TargetAudienceAr { get; set; } = string.Empty;
    public string? TargetAudienceEn { get; set; }
    public decimal TotalHours { get; set; }
    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public CourseOrigin Origin { get; set; }
    public long? ProposingCenterId { get; set; }
    public long? ProposedByUserId { get; set; }
    public AudienceType AudienceType { get; set; }
    public long? TargetCenterId { get; set; }
    public DateTimeOffset? RegistrationDeadline { get; set; }
    public int? MaxParticipants { get; set; }
    public bool HasExam { get; set; }
    public CourseStatus Status { get; set; }
    public DateTimeOffset StatusChangedAt { get; set; }
    public long? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateOnly? EvaluationEndDate { get; set; }
    public DateOnly? ImpactEndDate { get; set; }
    public long? EvaluationFormVersionId { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long? FinishedBy { get; set; }
    public int? FinalLecturesCount { get; set; }
    public short? AttendanceThresholdPercentOverride { get; set; }
    public bool ManagedByQuality { get; set; }
    public InstructorKind InstructorKind { get; set; }
    public long? InstructorUserId { get; set; }
    public string InstructorNameAr { get; set; } = string.Empty;
    public string? InstructorNameEn { get; set; }
    public string? InstructorOrgAr { get; set; }
    public string? InstructorOrgEn { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public long? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
    public bool IsHistorical { get; set; }
    public string? HistoricalNotes { get; set; }
    public int CurrentRevisionNo { get; set; }
    public string? LastReturnNotes { get; set; }
    public string? DeactivationCertificatesAction { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public long? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public long? ArchivedByUserId { get; set; }
    public TrainingTrack? Track { get; set; }
    public Center? ProposingCenter { get; set; }
    public User? ProposedByUser { get; set; }
    public Center? TargetCenter { get; set; }
    public EvaluationFormVersion? EvaluationFormVersion { get; set; }
    public User? InstructorUser { get; set; }
}

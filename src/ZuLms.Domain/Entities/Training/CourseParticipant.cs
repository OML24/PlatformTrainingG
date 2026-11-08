using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class CourseParticipant : BaseEntity
{
    public long CourseId { get; set; }
    public long UserId { get; set; }
    public ParticipantSource Source { get; set; }
    public long? AddedByUserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public long? RemovedBy { get; set; }
    public string? RemovalReason { get; set; }
    public string? Notes { get; set; }
    // Historical outcome is null for normal courses, true for completed/passed and
    // false for a historical attendee who did not earn the certificate.
    public bool? HistoricalPassed { get; set; }
    // Administrative certificate override marks the participant's course record as
    // completed and eligible. Evaluation may still be required before applying it.
    public bool AdminCertificateOverride { get; set; }
    public bool EvaluationExempt { get; set; }
    public Course? Course { get; set; }
    public User? User { get; set; }
    public User? AddedByUser { get; set; }
}

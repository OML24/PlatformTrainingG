using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class ExamResult : BaseEntity
{
    public long ExamId { get; set; }
    public long ParticipantId { get; set; }
    public ExamOutcome Outcome { get; set; }
    public bool IsActive { get; set; }
    public long RecordedBy { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset? ChangedAt { get; set; }
    public long? DeactivatedBy { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
    public string? DeactivationReason { get; set; }
    public CourseExam? Exam { get; set; }
    public CourseParticipant? Participant { get; set; }
    public User? RecordedByUser { get; set; }
    public User? DeactivatedByUser { get; set; }
}


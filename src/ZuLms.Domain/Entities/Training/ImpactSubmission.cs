using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class ImpactSubmission : BaseEntity, ISoftDeletable
{
    public long ParticipantId { get; set; }
    public int SequenceNo { get; set; }
    public ImpactType ImpactType { get; set; }
    public long FileId { get; set; }
    public string FileSha256 { get; set; } = string.Empty;
    public ImpactStatus Status { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public long? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? InstructorNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public long? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
    public CourseParticipant? Participant { get; set; }
    public StoredFile? File { get; set; }
    public User? DecidedByUser { get; set; }
}


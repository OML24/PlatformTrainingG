using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Needs;

public sealed class TrainingNeed : BaseEntity, ISoftDeletable
{
    public long CenterId { get; set; }
    public long SubmittedBy { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string DescriptionAr { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public long? TrackId { get; set; }
    public int? ExpectedParticipants { get; set; }
    public string? PreferredTimeframeAr { get; set; }
    public long? RequestedInstructorUserId { get; set; }
    public string? RequestedInstructorNoteAr { get; set; }
    public long LetterFileId { get; set; }
    public NeedStatus Status { get; set; }
    public int CurrentRevision { get; set; }
    public string? LastReturnNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public long? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
    public Center? Center { get; set; }
    public User? SubmittedByUser { get; set; }
    public TrainingTrack? Track { get; set; }
    public User? RequestedInstructorUser { get; set; }
    public StoredFile? LetterFile { get; set; }
}


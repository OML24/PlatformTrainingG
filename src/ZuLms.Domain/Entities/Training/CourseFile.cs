using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class CourseFile : BaseEntity, ISoftDeletable
{
    public long CourseId { get; set; }
    public long FileId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public long? LectureId { get; set; }
    public int? RevisionNo { get; set; }
    public string? Title { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public long? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
    public Course? Course { get; set; }
    public StoredFile? File { get; set; }
    public Lecture? Lecture { get; set; }
}


using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Files;

public sealed class VaultDocument : BaseEntity, ISoftDeletable
{
    public long FileId { get; set; }
    public FileKind DocType { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public long? RelatedCourseId { get; set; }
    public long? RelatedCenterId { get; set; }
    public string? SourceEntityType { get; set; }
    public long? SourceEntityId { get; set; }
    public long? UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public long? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
    public StoredFile? File { get; set; }
    public Course? RelatedCourse { get; set; }
    public Center? RelatedCenter { get; set; }
    public User? UploadedByUser { get; set; }
}


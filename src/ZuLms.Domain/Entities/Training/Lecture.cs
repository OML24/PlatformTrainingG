using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class Lecture : BaseEntity, ISoftDeletable
{
    public long CourseId { get; set; }
    public string? TitleAr { get; set; }
    public string? TitleEn { get; set; }
    public LectureMode Mode { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string? LocationAr { get; set; }
    public string? LocationEn { get; set; }
    public string? TeamsUrl { get; set; }
    public string AttendanceCode { get; set; } = string.Empty;
    public DateTimeOffset? CodeActivatedAt { get; set; }
    public DateTimeOffset? CodeExpiresAt { get; set; }
    public string? NotesAr { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public long? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
    public Course? Course { get; set; }
}


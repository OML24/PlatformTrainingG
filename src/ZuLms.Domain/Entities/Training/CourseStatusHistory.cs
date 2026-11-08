using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class CourseStatusHistory
{
    public long Id { get; set; }
    public long CourseId { get; set; }
    public CourseStatus? FromStatus { get; set; }
    public CourseStatus ToStatus { get; set; }
    public long? ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string? Reason { get; set; }
    public Course? Course { get; set; }
    public User? ChangedByUser { get; set; }
}


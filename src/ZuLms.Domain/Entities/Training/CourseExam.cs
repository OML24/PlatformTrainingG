using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class CourseExam : BaseEntity
{
    public long CourseId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public bool IsActive { get; set; }
    public Course? Course { get; set; }
}


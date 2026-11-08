using ZuLms.Domain.Common;

namespace ZuLms.Domain.Entities.Org;

public sealed class Department : BaseEntity, ISoftDeletable
{
    public int SideCode { get; set; }
    public UniversitySide Side { get; set; } = null!;
    public int DepartmentCode { get; set; }
    public int? CenterCode { get; set; }
    public long? CenterId { get; set; }
    public Center? Center { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? RegCode { get; set; }
    public int? HeadEmployeeCode { get; set; }
    public bool SourceMissing { get; set; }
    public DateTimeOffset? SourceMissingSince { get; set; }
    public string? SourceHash { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public long? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
}

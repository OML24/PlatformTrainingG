using ZuLms.Domain.Common;

namespace ZuLms.Domain.Entities.Org;

public sealed class Employee : BaseEntity
{
    public int SideCode { get; set; }
    public UniversitySide Side { get; set; } = null!;
    public int EmployeeCode { get; set; }
    public string? FirstName { get; set; }
    public string? SecondName { get; set; }
    public string? ThirdName { get; set; }
    public string? LastName { get; set; }
    public string FullNameAr { get; set; } = string.Empty;
    public string? FullNameEn { get; set; }
    public int? CenterCode { get; set; }
    public int? DepartmentCode { get; set; }
    public long? CenterId { get; set; }
    public Center? Center { get; set; }
    public long? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? MainJobCode { get; set; }
    public string? MainJobDesc { get; set; }
    public int? SubJobCode { get; set; }
    public string? SubJobDesc { get; set; }
    public int? EmpStatus { get; set; }
    public string? EmpStatusDesc { get; set; }
    public int? ChkEmpStat { get; set; }
    public DateOnly? StartWorkDate { get; set; }
    public DateOnly? StopWorkDate { get; set; }
    public string? SourceEmail { get; set; }
    public bool IsAcademic { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public bool SourceMissing { get; set; }
    public DateTimeOffset? SourceMissingSince { get; set; }
    public string? SourceHash { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
}

using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Identity;

public sealed class Role
{
    public long Id { get; set; }
    public RoleCode Code { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public bool IsSystem { get; set; }
    public bool IsAssignable { get; set; }
    public int SortOrder { get; set; }
}


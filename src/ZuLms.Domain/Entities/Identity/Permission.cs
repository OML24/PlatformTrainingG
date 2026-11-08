using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Identity;

public sealed class Permission
{
    public long Id { get; set; }
    public string PermKey { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public PermissionKind Kind { get; set; }
    public PermissionScope Scope { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}


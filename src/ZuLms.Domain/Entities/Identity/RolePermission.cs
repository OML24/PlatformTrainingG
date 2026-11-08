using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Identity;

public sealed class RolePermission
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}


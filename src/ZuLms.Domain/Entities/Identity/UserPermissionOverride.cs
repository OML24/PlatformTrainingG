using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Identity;

public sealed class UserPermissionOverride : BaseEntity
{
    public long UserId { get; set; }
    public long PermissionId { get; set; }
    public OverrideMode Mode { get; set; }
    public string? Reason { get; set; }
    public User? User { get; set; }
    public Permission? Permission { get; set; }
}


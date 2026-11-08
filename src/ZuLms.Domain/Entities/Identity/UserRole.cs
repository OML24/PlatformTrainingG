using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Identity;

public sealed class UserRole : BaseEntity
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public long? ScopeCenterId { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public long? RevokedBy { get; set; }
    public User? User { get; set; }
    public Role? Role { get; set; }
    public Center? ScopeCenter { get; set; }
}


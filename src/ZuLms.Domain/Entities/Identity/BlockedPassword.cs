using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Identity;

public sealed class BlockedPassword
{
    public long Id { get; set; }
    public string PasswordNormalized { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public long? CreatedBy { get; set; }
    public User? CreatedByUser { get; set; }
}


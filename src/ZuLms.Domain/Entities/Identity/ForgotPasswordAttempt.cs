using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Identity;

public sealed class ForgotPasswordAttempt
{
    public long Id { get; set; }
    public string LoginNameInput { get; set; } = string.Empty;
    public string EmailInput { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public User? User { get; set; }
}


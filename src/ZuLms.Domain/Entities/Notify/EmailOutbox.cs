using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Notify;

public sealed class EmailOutbox
{
    public long Id { get; set; }
    public EmailKind Kind { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public long? RelatedUserId { get; set; }
    public OutboxStatus Status { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public string? ProviderMessageId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public User? RelatedUser { get; set; }
}


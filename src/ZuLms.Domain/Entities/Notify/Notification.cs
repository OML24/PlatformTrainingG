using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Notify;

public sealed class Notification
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public NotificationType Type { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string BodyAr { get; set; } = string.Empty;
    public string? BodyEn { get; set; }
    public string? LinkUrl { get; set; }
    public string? RelatedEntityType { get; set; }
    public long? RelatedEntityId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public bool IsActive { get; set; }
    public User? User { get; set; }
}


using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Needs;

public sealed class TrainingNeedEvent
{
    public long Id { get; set; }
    public long NeedId { get; set; }
    public string Event { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public long ByUserId { get; set; }
    public DateTimeOffset At { get; set; }
    public TrainingNeed? Need { get; set; }
    public User? ByUser { get; set; }
}


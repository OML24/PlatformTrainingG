using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Sync;

public sealed class SyncRun
{
    public long Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long? TriggeredBy { get; set; }
    public SyncStatus Status { get; set; }
    public string StatsJson { get; set; } = string.Empty;
    public string? ErrorText { get; set; }
    public User? TriggeredByUser { get; set; }
}


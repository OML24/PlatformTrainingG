using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Sync;

public sealed class SyncReviewItem
{
    public long Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityKey { get; set; } = string.Empty;
    public SyncReviewReason Reason { get; set; }
    public string? Details { get; set; }
    public long? FirstSeenRunId { get; set; }
    public SyncReviewOrigin Origin { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public long? ResolvedBy { get; set; }
    public SyncReviewResolution? Resolution { get; set; }
    public string? Notes { get; set; }
    public SyncRun? FirstSeenRun { get; set; }
    public User? ResolvedByUser { get; set; }
}


using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Audit;

public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public long? ActorUserId { get; set; }
    public string? ActorLogin { get; set; }
    public string? ActorName { get; set; }
    public string? ActorRoles { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceSummary { get; set; }
    public AuditCategory Category { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public long? EntityId { get; set; }
    public string? EntityLabel { get; set; }
    public long? CourseId { get; set; }
    public string SummaryAr { get; set; } = string.Empty;
    public string? SummaryEn { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public string? CorrelationId { get; set; }
    public User? ActorUser { get; set; }
    public Course? Course { get; set; }
}


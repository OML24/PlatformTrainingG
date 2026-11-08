using ZuLms.Domain.Enums;

namespace ZuLms.Application.Common.Abstractions;

public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken);
}

public sealed record AuditEntry(
    AuditCategory Category, string ActionCode, string SummaryAr, string? SummaryEn = null,
    string? EntityType = null, long? EntityId = null, string? EntityLabel = null,
    long? CourseId = null, string? BeforeJson = null, string? AfterJson = null,
    bool Success = true, string? FailureReason = null, string? CorrelationId = null,
    long? ActorUserId = null, string? ActorLogin = null, string? ActorName = null,
    string? ActorRoles = null);

using System.Text.Json.Nodes;
using ZuLms.Application.Common.Abstractions;

namespace ZuLms.Infrastructure.Persistence;

public sealed class EfAuditLogger(AppDbContext db, ICurrentUser currentUser, IClock clock) : IAuditLogger
{
    public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = clock.UtcNow,
            ActorUserId = entry.ActorUserId ?? currentUser.UserId,
            ActorLogin = entry.ActorLogin ?? currentUser.LoginName,
            ActorName = entry.ActorName,
            ActorRoles = entry.ActorRoles ?? string.Join(',', currentUser.Roles),
            Ip = currentUser.Ip,
            UserAgent = currentUser.UserAgent.Length > 500 ? currentUser.UserAgent[..500] : currentUser.UserAgent,
            Category = entry.Category,
            ActionCode = entry.ActionCode,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            EntityLabel = entry.EntityLabel,
            CourseId = entry.CourseId,
            SummaryAr = entry.SummaryAr,
            SummaryEn = entry.SummaryEn,
            BeforeJson = Redact(entry.BeforeJson),
            AfterJson = Redact(entry.AfterJson),
            Success = entry.Success,
            FailureReason = entry.FailureReason,
            CorrelationId = entry.CorrelationId
        });
        return Task.CompletedTask;
    }

    private static string? Redact(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        JsonNode node = JsonNode.Parse(json) ?? throw new ArgumentException("Audit JSON is invalid.");
        Walk(node);
        return node.ToJsonString();
    }

    private static void Walk(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (string key in obj.Select(x => x.Key).ToArray())
            {
                if (IsSensitive(key)) obj[key] = "[محذوف]";
                else if (obj[key] is { } child) Walk(child);
            }
        }
        else if (node is JsonArray array)
            foreach (JsonNode? child in array)
                if (child is not null) Walk(child);
    }

    private static bool IsSensitive(string key) =>
        new[] { "password", "secret", "token", "authorization", "credential", "body_html", "key" }
            .Any(term => key.Contains(term, StringComparison.OrdinalIgnoreCase));
}

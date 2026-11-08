using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Enums;
using ZuLms.Infrastructure.Email;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Background;

public sealed class EmailOutboxWorker(IServiceScopeFactory scopes, IEmailSender sender, IClock clock,
    Microsoft.Extensions.Logging.ILogger<EmailOutboxWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!sender.IsConfigured)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                continue;
            }
            try
            {
                bool processed = await ProcessOneAsync(stoppingToken);
                if (!processed) await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("Email outbox worker failed with {ErrorType}", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var due = await db.EmailOutboxs.FromSqlInterpolated($"""
            SELECT * FROM zulms_rebuild.email_outbox
            WHERE status = 'Pending' AND next_attempt_at <= {clock.UtcNow}
            ORDER BY created_at, id LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        var message = due.FirstOrDefault();
        if (message is null) return false;
        string fromAddress = await settings.GetAsync<string>("email.from_address", ct);
        string fromName = await settings.GetAsync<string>("email.from_name_ar", ct);
        int maxAttempts = await settings.GetAsync<int>("email.max_attempts", ct);
        int throttleMs = await settings.GetAsync<int>("email.throttle_ms", ct);
        var (outcome, error) = await sender.SendAsync(message, fromAddress, fromName, ct);
        message.Attempts++;
        message.LastError = error;
        switch (outcome)
        {
            case EmailSendOutcome.Sent:
                message.Status = OutboxStatus.Sent;
                message.SentAt = clock.UtcNow;
                message.BodyHtml = "[محذوف]";
                break;
            case EmailSendOutcome.Failed:
                message.Status = OutboxStatus.Failed;
                break;
            case EmailSendOutcome.Uncertain:
                message.Status = OutboxStatus.Uncertain;
                break;
            case EmailSendOutcome.Retry:
                if (message.Attempts >= maxAttempts) message.Status = OutboxStatus.Failed;
                else message.NextAttemptAt = clock.UtcNow.AddMilliseconds(throttleMs);
                break;
        }
        if (message.Kind is (EmailKind.InitialCredentials or EmailKind.ExternalInstructorAccount) &&
            message.RelatedUserId is long userId)
        {
            var user = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == userId, ct);
            if (user is not null)
            {
                user.CredentialsEmailAttempts++;
                user.CredentialsEmailLastError = message.LastError;
                if (message.Status != OutboxStatus.Pending)
                {
                    user.CredentialsEmailStatus = message.Status switch
                    {
                        OutboxStatus.Sent => CredentialsEmailStatus.Sent,
                        OutboxStatus.Failed => CredentialsEmailStatus.Failed,
                        _ => CredentialsEmailStatus.Uncertain
                    };
                    user.CredentialsEmailSentAt = message.Status == OutboxStatus.Sent
                        ? message.SentAt : null;
                }
            }
        }
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
        await audit.LogAsync(new AuditEntry(AuditCategory.System, "email.outbox_processed",
            "معالجة رسالة بريد", "Email outbox message processed", "EmailOutbox", message.Id,
            Success: message.Status == OutboxStatus.Sent,
            FailureReason: message.Status == OutboxStatus.Sent ? null : message.Status.ToString()), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (throttleMs > 0) await Task.Delay(TimeSpan.FromMilliseconds(throttleMs), ct);
        return true;
    }
}

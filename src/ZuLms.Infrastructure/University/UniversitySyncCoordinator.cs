using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Sync;
using ZuLms.Domain.Entities.Sync;
using ZuLms.Domain.Enums;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.University;

public sealed class UniversitySyncCoordinator(IServiceScopeFactory scopes,
    UniversitySyncLock syncLock, ILogger<UniversitySyncCoordinator> logger)
    : BackgroundService, IUniversitySyncService
{
    private static readonly JsonSerializerOptions StatsJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Channel<SyncJob> jobs = Channel.CreateUnbounded<SyncJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public async Task<Result<long>> StartAsync(long actorUserId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionService>();
        if (!await permissions.HasAsync(actorUserId, "sync.run", ct))
            return Result<long>.Failure("FORBIDDEN");
        var lease = await syncLock.TryAcquireAsync(ct);
        if (lease is null) return Result<long>.Failure("SYNC_ALREADY_RUNNING");
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            await MarkInterruptedRunsAsync(db, clock, ct);
            var run = new SyncRun
            {
                StartedAt = clock.UtcNow, TriggeredBy = actorUserId,
                Status = SyncStatus.Partial, StatsJson = "{}"
            };
            db.SyncRuns.Add(run);
            await db.SaveChangesAsync(ct);
            if (!jobs.Writer.TryWrite(new SyncJob(run.Id, lease)))
                throw new InvalidOperationException("Synchronization queue is unavailable.");
            return Result<long>.Success(run.Id);
        }
        catch
        {
            await lease.DisposeAsync();
            throw;
        }
    }

    public async Task<Result<SyncRunDto>> GetRunAsync(long actorUserId, long runId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionService>();
        if (!await permissions.HasAsync(actorUserId, "page.admin.sync", ct))
            return Result<SyncRunDto>.Failure("FORBIDDEN");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = await db.SyncRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == runId, ct);
        return run is null ? Result<SyncRunDto>.Failure("SYNC_RUN_NOT_FOUND") :
            Result<SyncRunDto>.Success(new(run.Id, run.StartedAt, run.FinishedAt,
                run.Status, run.StatsJson, run.ErrorText, run.TriggeredBy));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var recoveryLease = await syncLock.TryAcquireAsync(stoppingToken);
            if (recoveryLease is not null)
            {
                await using var scope = scopes.CreateAsyncScope();
                await MarkInterruptedRunsAsync(
                    scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Sync recovery failed with {ErrorType}", ex.GetType().Name);
        }
        await foreach (var job in jobs.Reader.ReadAllAsync(stoppingToken))
        {
            await using (job.Lease)
            {
                try { await RunAsync(job.Id, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    await FinishAsync(job.Id, SyncStatus.Failed, "SYNC_CANCELLED", "{}", CancellationToken.None);
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError("University sync run {RunId} failed with {ErrorType}",
                        job.Id, ex.GetType().Name);
                    await FinishAsync(job.Id, SyncStatus.Failed, ex.GetType().Name, "{}", CancellationToken.None);
                }
            }
        }
    }

    private static async Task MarkInterruptedRunsAsync(AppDbContext db, IClock clock,
        CancellationToken ct)
    {
        var unfinished = await db.SyncRuns
            .Where(x => x.FinishedAt == null).ToListAsync(ct);
        if (unfinished.Count == 0) return;
        DateTimeOffset now = clock.UtcNow;
        foreach (var run in unfinished)
        {
            run.Status = SyncStatus.Failed;
            run.FinishedAt = now;
            run.ErrorText = "SYNC_INTERRUPTED";
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task RunAsync(long runId, CancellationToken ct)
    {
        var stats = new Dictionary<string, SourceRunStats>(StringComparer.Ordinal);
        int successful = 0;
        foreach (UniversitySource source in Enum.GetValues<UniversitySource>())
        {
            string key = source.ToString().ToLowerInvariant();
            try
            {
                (SourceMergeStats result, IReadOnlyList<SyncError> warnings) =
                    await RunSourceAsync(source, runId, ct);
                stats[key] = new(result.Fetched, result.Added, result.Updated,
                    result.Unchanged, result.Missing, warnings);
                successful++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                string code = ex switch
                {
                    UniversitySourceException sourceError => sourceError.Code,
                    InvalidDataException dataError => dataError.Message,
                    _ => ex.GetType().Name
                };
                stats[key] = new(0, 0, 0, 0, 0, [new SyncError(source.ToString(), code)]);
                logger.LogWarning("University sync source {Source} failed with {Code}", source, code);
            }
            await SaveProgressAsync(runId, stats, ct);
        }
        int accountsCreated = 0;
        if (stats.TryGetValue("employees", out var employees) && employees.Errors.Count == 0)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var provisioning = scope.ServiceProvider.GetRequiredService<UniversityProvisioningService>();
                accountsCreated = (await provisioning.ApplyAsync(runId, ct)).Created;
            }
            catch (Exception ex)
            {
                stats["provisioning"] = new(0, 0, 0, 0, 0,
                    [new SyncError("Provisioning", ex.GetType().Name)]);
                logger.LogWarning("University provisioning failed with {ErrorType}", ex.GetType().Name);
            }
        }
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var merge = scope.ServiceProvider.GetRequiredService<UniversityMergeService>();
            await merge.DetectSharedEmailsAsync(runId, ct);
        }
        catch (Exception ex)
        {
            stats["sharedEmails"] = new(0, 0, 0, 0, 0,
                [new SyncError("SharedEmail", ex.GetType().Name)]);
            logger.LogWarning("Shared email detection failed with {ErrorType}", ex.GetType().Name);
        }
        int reviewItemsCreated;
        DateTimeOffset startedAt;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            reviewItemsCreated = await db.SyncReviewItems.CountAsync(x => x.FirstSeenRunId == runId, ct);
            startedAt = await db.SyncRuns.Where(x => x.Id == runId)
                .Select(x => x.StartedAt).SingleAsync(ct);
        }
        DateTimeOffset finishedAt;
        await using (var scope = scopes.CreateAsyncScope())
            finishedAt = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
        var finalStats = JsonSerializer.Serialize(new
        {
            sources = stats, accountsCreated, reviewItemsCreated, startedAt, finishedAt
        }, StatsJsonOptions);
        SyncStatus status = successful == Enum.GetValues<UniversitySource>().Length && !stats.ContainsKey("provisioning") &&
            !stats.ContainsKey("sharedEmails")
            ? SyncStatus.Success : successful == 0 ? SyncStatus.Failed : SyncStatus.Partial;
        await FinishAsync(runId, status, null, finalStats, ct, finishedAt);
    }

    private async Task<(SourceMergeStats Stats, IReadOnlyList<SyncError> Warnings)> RunSourceAsync(
        UniversitySource source, long runId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IUniversityApiClient>();
        var merge = scope.ServiceProvider.GetRequiredService<UniversityMergeService>();
        var rows = await client.FetchAsync(source, ct);
        List<SyncError> warnings = [];
        SourceMergeStats stats = source switch
        {
            UniversitySource.Centers => await merge.ApplyCentersAsync(
                rows.Select(UniversitySourceMapper.Center).ToList(), runId, ct),
            UniversitySource.Departments => await merge.ApplyDepartmentsAsync(
                rows.Select(UniversitySourceMapper.Department).ToList(), runId, ct),
            UniversitySource.Employees => await merge.ApplyEmployeesAsync(
                rows.Select(UniversitySourceMapper.Employee).ToList(), runId, ct),
            UniversitySource.Emails => await ApplyEmailsAsync(merge, rows, runId, ct, warnings),
            UniversitySource.Programs => await merge.ApplyProgramsAsync(
                rows.Select(UniversitySourceMapper.Program).ToList(), runId, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        return (stats, warnings);
    }

    private async Task<SourceMergeStats> ApplyEmailsAsync(UniversityMergeService merge,
        IReadOnlyList<JsonElement> rows, long runId, CancellationToken ct, List<SyncError> warnings)
    {
        var emails = new List<EmailSource>(rows.Count);
        int skipped = 0;
        foreach (var row in rows)
        {
            if (UniversitySourceMapper.TryEmail(row, out var email)) emails.Add(email);
            else skipped++;
        }
        SourceMergeStats stats = await merge.ApplyEmailsAsync(emails, runId, ct);
        if (skipped == 0) return stats;
        logger.LogWarning("Skipped {SkippedRows} email rows without side_id/employee_id", skipped);
        warnings.Add(new SyncError(nameof(UniversitySource.Emails), $"SKIPPED_INVALID_ROWS:{skipped}"));
        return stats with { Fetched = rows.Count };
    }

    private async Task SaveProgressAsync(long runId, Dictionary<string, SourceRunStats> stats,
        CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = await db.SyncRuns.SingleAsync(x => x.Id == runId, ct);
        run.StatsJson = JsonSerializer.Serialize(new { sources = stats, startedAt = run.StartedAt },
            StatsJsonOptions);
        await db.SaveChangesAsync(ct);
    }

    private async Task FinishAsync(long runId, SyncStatus status, string? error,
        string stats, CancellationToken ct, DateTimeOffset? finishedAt = null)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var run = await db.SyncRuns.SingleAsync(x => x.Id == runId, ct);
        run.Status = status;
        run.FinishedAt = finishedAt ?? clock.UtcNow;
        run.StatsJson = stats;
        run.ErrorText = error;
        await db.SaveChangesAsync(ct);
    }

    private sealed record SyncJob(long Id, IAsyncDisposable Lease);
    private sealed record SyncError(string Key, string Message);
    private sealed record SourceRunStats(int Fetched, int Added, int Updated,
        int Unchanged, int Missing, IReadOnlyList<SyncError> Errors);
}

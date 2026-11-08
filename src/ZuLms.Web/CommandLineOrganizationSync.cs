using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Features.Sync;
using ZuLms.Domain.Entities.Sync;
using ZuLms.Domain.Enums;
using ZuLms.Infrastructure.Persistence;
using ZuLms.Infrastructure.University;

internal static class CommandLineOrganizationSync
{
    public static async Task RunAsync(WebApplication app)
    {
        string login = Environment.GetEnvironmentVariable("ZU_SYNC_ACTOR_LOGIN")
            ?? Environment.GetEnvironmentVariable("ZU_BOOTSTRAP_ADMIN_LOGIN")
            ?? throw new InvalidOperationException("A sync actor login is required.");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionService>();
        long actor = await db.Users.AsNoTracking().Where(x => x.LoginName == login && x.IsActive)
            .Select(x => x.Id).SingleOrDefaultAsync();
        if (actor == 0 || !await permissions.HasAsync(actor, "sync.run", CancellationToken.None))
            throw new InvalidOperationException("Sync actor is missing or lacks sync.run.");
        await using var lease = await scope.ServiceProvider.GetRequiredService<UniversitySyncLock>()
            .TryAcquireAsync(CancellationToken.None)
            ?? throw new InvalidOperationException("SYNC_ALREADY_RUNNING");
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var run = new SyncRun { StartedAt = clock.UtcNow, TriggeredBy = actor,
            Status = SyncStatus.Partial, StatsJson = "{\"scope\":\"organization-only\"}" };
        db.SyncRuns.Add(run);
        await db.SaveChangesAsync();
        try
        {
            var client = scope.ServiceProvider.GetRequiredService<IUniversityApiClient>();
            // Fetch and parse all three sources before any organization changes.
            var centers = (await client.FetchAsync(UniversitySource.Centers, CancellationToken.None))
                .Select(UniversitySourceMapper.Center).ToList();
            var departments = (await client.FetchAsync(UniversitySource.Departments, CancellationToken.None))
                .Select(UniversitySourceMapper.Department).ToList();
            var programs = (await client.FetchAsync(UniversitySource.Programs, CancellationToken.None))
                .Select(UniversitySourceMapper.Program).ToList();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var merge = scope.ServiceProvider.GetRequiredService<UniversityMergeService>();
            var centerStats = await merge.ApplyCentersAsync(centers, run.Id, CancellationToken.None);
            var departmentStats = await merge.ApplyDepartmentsAsync(departments, run.Id, CancellationToken.None);
            var programStats = await merge.ApplyProgramsAsync(programs, run.Id, CancellationToken.None);
            int employeesReconciled = await merge.ReconcileEmployeeOrganizationAsync(run.Id, CancellationToken.None);
            run.StatsJson = JsonSerializer.Serialize(new { scope = "organization-only",
                sources = new { centers = centerStats, departments = departmentStats, programs = programStats },
                employeesReconciled, accountsCreated = 0 });
            run.Status = SyncStatus.Success;
            run.FinishedAt = clock.UtcNow;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            Console.WriteLine($"Organization sync #{run.Id}: {run.StatsJson}");
        }
        catch (Exception ex)
        {
            // Do not accidentally persist tracked organization edits after rollback.
            db.ChangeTracker.Clear();
            var failed = await db.SyncRuns.SingleAsync(x => x.Id == run.Id);
            failed.Status = SyncStatus.Failed;
            failed.FinishedAt = clock.UtcNow;
            failed.ErrorText = ex.GetType().Name;
            await db.SaveChangesAsync();
            throw;
        }
    }
}

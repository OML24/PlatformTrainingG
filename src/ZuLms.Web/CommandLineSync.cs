using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Features.Sync;
using ZuLms.Infrastructure.Persistence;

internal static class CommandLineSync
{
    public static async Task RunAsync(WebApplication app)
    {
        string login = Environment.GetEnvironmentVariable("ZU_SYNC_ACTOR_LOGIN")
            ?? Environment.GetEnvironmentVariable("ZU_BOOTSTRAP_ADMIN_LOGIN")
            ?? throw new InvalidOperationException("ZU_SYNC_ACTOR_LOGIN or ZU_BOOTSTRAP_ADMIN_LOGIN is required.");
        await app.StartAsync();
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            long actor = await db.Users.AsNoTracking()
                .Where(x => x.LoginName == login && x.IsActive)
                .Select(x => x.Id).SingleOrDefaultAsync();
            if (actor == 0) throw new InvalidOperationException("Sync actor was not found or is inactive.");

            var sync = scope.ServiceProvider.GetRequiredService<IUniversitySyncService>();
            var start = await sync.StartAsync(actor, CancellationToken.None);
            if (!start.Ok) throw new InvalidOperationException($"Sync could not start: {start.Error?.Code}");
            long runId = start.Value;
            Console.WriteLine($"Sync run {runId} started.");
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                var run = await sync.GetRunAsync(actor, runId, CancellationToken.None);
                if (!run.Ok || run.Value is null)
                    throw new InvalidOperationException($"Sync status unavailable: {run.Error?.Code}");
                if (run.Value.FinishedAt is null) continue;
                Console.WriteLine($"Sync run {runId} finished: {run.Value.Status}.");
                if (run.Value.Status != ZuLms.Domain.Enums.SyncStatus.Success)
                    Environment.ExitCode = 1;
                break;
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }
}

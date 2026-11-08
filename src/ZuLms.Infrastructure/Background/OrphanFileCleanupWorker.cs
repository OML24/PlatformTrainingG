using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Background;

public sealed class OrphanFileCleanupWorker(IServiceScopeFactory scopes,
    ILogger<OrphanFileCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the application time to start before the first maintenance pass.
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CleanAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Orphan file cleanup failed"); }
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task CleanAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM zulms_rebuild.rate_windows WHERE window_start < now() - interval '1 day';
            DELETE FROM zulms_rebuild.credential_reauth_grants WHERE expires_at < now();
            """, ct);
        var cutoff = DateTimeOffset.UtcNow - Retention;
        var candidates = await db.StoredFiles.Where(file => file.UploadedAt < cutoff &&
            !db.CourseFiles.Any(x => x.FileId == file.Id) &&
            !db.CourseCancellationRequests.Any(x => x.LetterFileId == file.Id) &&
            !db.TrainingNeeds.Any(x => x.LetterFileId == file.Id) &&
            !db.ImpactSubmissions.Any(x => x.FileId == file.Id) &&
            !db.ProfessionalCertifications.Any(x => x.FileId == file.Id) &&
            !db.MemberProfiles.Any(x => x.PhotoFileId == file.Id) &&
            !db.ExternalInstructorProfiles.Any(x => x.CvFileId == file.Id) &&
            !db.Certificates.Any(x => x.PdfFileId == file.Id) &&
            !db.CertificateTemplates.Any(x => x.FileId == file.Id) &&
            !db.VaultDocuments.Any(x => x.FileId == file.Id))
            .OrderBy(x => x.Id).Take(100).ToListAsync(ct);
        foreach (var file in candidates)
        {
            if (file.IsActive)
            {
                file.IsActive = false;
                await db.SaveChangesAsync(ct);
            }
            try
            {
                await storage.DeleteUncommittedAsync(file.RelativePath);
                logger.LogInformation("Removed orphan file {FileId}", file.Id);
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Could not delete orphan file {FileId}", file.Id);
            }
        }
    }
}

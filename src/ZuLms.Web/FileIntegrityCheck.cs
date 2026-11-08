using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Infrastructure.Persistence;

internal static class FileIntegrityCheck
{
    public static async Task RunAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var files = await db.StoredFiles.AsNoTracking().Where(x => x.IsActive)
            .Select(x => new { x.Id, x.RelativePath }).ToListAsync();
        int missing = 0;
        foreach (var file in files)
        {
            try { await using var stream = await storage.OpenReadAsync(file.RelativePath, CancellationToken.None); }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"Missing file content: #{file.Id} ({file.RelativePath})");
                missing++;
            }
        }
        Console.WriteLine($"Checked {files.Count} files; {missing} missing.");
        if (missing > 0) Environment.ExitCode = 2;
    }
}

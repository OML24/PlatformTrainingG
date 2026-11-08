using ZuLms.Application.Common.Abstractions;

namespace ZuLms.Infrastructure.Files;

public sealed class DiskFileStorage : IFileStorage
{
    private readonly string root;

    public DiskFileStorage()
    {
        string configured = Environment.GetEnvironmentVariable("ZU_STORAGE_ROOT")
            ?? throw new InvalidOperationException("ZU_STORAGE_ROOT is required.");
        if (!Path.IsPathFullyQualified(configured))
            throw new InvalidOperationException("ZU_STORAGE_ROOT must be an absolute path.");
        root = Path.GetFullPath(configured);
    }

    public async Task<string> WriteAsync(string extension, ReadOnlyMemory<byte> content,
        DateTimeOffset now, CancellationToken ct)
    {
        if (extension.Length is < 2 or > 6 || extension[0] != '.' ||
            !extension.AsSpan(1).ToString().All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException("Invalid file extension.", nameof(extension));
        string relative = Path.Combine(now.Year.ToString("D4"), now.Month.ToString("D2"),
            $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}");
        string absolute = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        try
        {
            await using var stream = new FileStream(absolute, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await stream.WriteAsync(content, ct);
            await stream.FlushAsync(ct);
        }
        catch
        {
            File.Delete(absolute);
            throw;
        }
        return relative.Replace('\\', '/');
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string absolute = Resolve(relativePath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException("Stored file content was not found.", absolute);
        Stream stream = new FileStream(absolute, FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous);
        return Task.FromResult(stream);
    }

    public Task DeleteUncommittedAsync(string relativePath)
    {
        File.Delete(Resolve(relativePath));
        return Task.CompletedTask;
    }

    private string Resolve(string relativePath)
    {
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        string prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("File path escapes storage root.");
        return path;
    }
}

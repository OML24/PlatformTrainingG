namespace ZuLms.Application.Common.Abstractions;

public interface IFileStorage
{
    Task<string> WriteAsync(string extension, ReadOnlyMemory<byte> content,
        DateTimeOffset now, CancellationToken ct);
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct);
    Task DeleteUncommittedAsync(string relativePath);
}

using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Files;

public interface IFileUploadService
{
    Task<Result<FileUploadDto>> UploadAsync(long actorUserId, FileKind kind,
        string originalName, Stream content, long declaredLength, CancellationToken ct);
    Task<Result<FileDownloadDto>> DownloadAsync(long actorUserId, long fileId,
        CancellationToken ct);
}

public sealed record FileUploadDto(long Id, FileKind Kind, string OriginalName,
    long SizeBytes);
public sealed record FileDownloadDto(Stream Content, string OriginalName,
    string MimeType);

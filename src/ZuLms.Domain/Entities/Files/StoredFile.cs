using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Files;

public sealed class StoredFile
{
    public long Id { get; set; }
    public FileKind Kind { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public long? UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public bool IsActive { get; set; }
    public User? UploadedByUser { get; set; }
}


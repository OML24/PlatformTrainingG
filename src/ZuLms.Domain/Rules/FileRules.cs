using System.IO.Compression;

namespace ZuLms.Domain.Rules;

public static class FileRules
{
    public static bool IsAllowed(string fileName, ReadOnlyMemory<byte> content, IReadOnlyCollection<string> allowedExtensions)
    {
        string extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return false;
        ReadOnlySpan<byte> bytes = content.Span;
        return extension switch
        {
            "pdf" => bytes.StartsWith("%PDF-"u8),
            "jpg" or "jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "png" => bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "ppt" => bytes.StartsWith(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
            "zip" => IsZip(bytes),
            "docx" or "pptx" or "xlsx" => IsOfficeArchive(content, extension),
            _ => false
        };
    }

    private static bool IsZip(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 });

    private static bool IsOfficeArchive(ReadOnlyMemory<byte> content, string extension)
    {
        if (!IsZip(content.Span)) return false;
        try
        {
            using var stream = new MemoryStream(content.ToArray(), writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            if (archive.GetEntry("[Content_Types].xml") is null) return false;
            string prefix = extension switch { "docx" => "word/", "pptx" => "ppt/", _ => "xl/" };
            return archive.Entries.Any(entry => entry.FullName.StartsWith(prefix, StringComparison.Ordinal));
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}

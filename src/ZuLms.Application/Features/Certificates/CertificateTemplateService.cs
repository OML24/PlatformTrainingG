using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Files;
using ZuLms.Domain.Entities.Certificates;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Certificates;

public sealed class CertificateTemplateService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings, IFileUploadService uploads,
    IFileStorage storage, ICertificatePdfRenderer renderer, IQrCodeService qr,
    ICertificateService certificates, IAuditLogger audit)
    : ICertificateTemplateService
{
    private static readonly string[] RequiredFields =
    [
        "holder_name", "course_title", "course_period", "course_hours",
        "instructor_name", "issue_date", "reference_no", "qr"
    ];

    public async Task<Result<CertificateTemplateDto>> GetCurrentAsync(
        long actorUserId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "cert_template.upload", ct))
            return Result<CertificateTemplateDto>.Failure("FORBIDDEN");
        var template = await db.CertificateTemplates.AsNoTracking()
            .Where(x => x.IsCurrent)
            .OrderByDescending(x => x.VersionNo)
            .Select(x => new CertificateTemplateDto(x.Id, x.VersionNo, x.FileId,
                x.PageWidthPt, x.PageHeightPt, x.LayoutJson, x.IsCurrent, x.CreatedAt))
            .FirstOrDefaultAsync(ct);
        if (template is null)
            return Result<CertificateTemplateDto>.Failure("CERT_TEMPLATE_MISSING");
        return Result<CertificateTemplateDto>.Success(template);
    }

    public async Task<Result<CertificateTemplateDto>> UploadAsync(long actorUserId,
        string fileName, Stream content, long declaredLength, string layoutJson,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "cert_template.upload", ct))
            return Result<CertificateTemplateDto>.Failure("FORBIDDEN");
        string? layoutError = ValidateLayout(layoutJson);
        if (layoutError is not null)
            return Result<CertificateTemplateDto>.Failure(layoutError);
        var upload = await uploads.UploadAsync(actorUserId, FileKind.CertificateTemplate,
            fileName, content, declaredLength, ct);
        if (!upload.Ok)
            return Result<CertificateTemplateDto>.Failure(upload.Error!.Code);
        byte[] pdf = await ReadStoredAsync(upload.Value!.Id, ct);
        (decimal width, decimal height)? size = PdfPageSize(pdf);
        if (size is null)
            return Result<CertificateTemplateDto>.Failure("CERT_TEMPLATE_INVALID_PDF");
        DateTimeOffset now = clock.UtcNow;
        var template = await db.InTransactionAsync(async () =>
        {
            int maxVersion = await db.CertificateTemplates.AsNoTracking()
                .Select(x => (int?)x.VersionNo).MaxAsync(ct) ?? 0;
            var previous = await db.CertificateTemplates
                .Where(x => x.IsCurrent).ToListAsync(ct);
            foreach (var old in previous) old.IsCurrent = false;
            var created = new CertificateTemplate
            {
                VersionNo = maxVersion + 1, FileId = upload.Value.Id,
                PageWidthPt = size!.Value.width, PageHeightPt = size.Value.height,
                LayoutJson = layoutJson, IsCurrent = true,
                CreatedAt = now, CreatedByUserId = actorUserId
            };
            db.CertificateTemplates.Add(created);
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(new AuditEntry(AuditCategory.Settings, "template.uploaded",
                "رفع قالب شهادة", "Certificate template uploaded", "CertificateTemplate",
                created.Id, $"v{created.VersionNo}",
                AfterJson: JsonSerializer.Serialize(new
                {
                    version = created.VersionNo,
                    width = created.PageWidthPt, height = created.PageHeightPt
                }), ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return created;
        }, ct);
        // First template upload releases every pending eligible certificate.
        await certificates.IssuePendingAsync(actorUserId, null, ct);
        return Result<CertificateTemplateDto>.Success(new CertificateTemplateDto(
            template.Id, template.VersionNo, template.FileId, template.PageWidthPt,
            template.PageHeightPt, template.LayoutJson, template.IsCurrent,
            template.CreatedAt));
    }

    public async Task<Result<byte[]>> PreviewAsync(long actorUserId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "cert_template.upload", ct))
            return Result<byte[]>.Failure("FORBIDDEN");
        var template = await db.CertificateTemplates.AsNoTracking()
            .Where(x => x.IsCurrent).OrderByDescending(x => x.VersionNo)
            .Select(x => new { x.PageWidthPt, x.PageHeightPt, x.LayoutJson, x.FileId })
            .FirstOrDefaultAsync(ct);
        if (template is null)
            return Result<byte[]>.Failure("CERT_TEMPLATE_MISSING");
        byte[] pdf = await ReadStoredAsync(template.FileId, ct);
        string verifyBase = await CertificateVerificationUrl.BaseAsync(
            settings, ct);
        var layer = new CertificateTextLayerInput(template.PageWidthPt,
            template.PageHeightPt, template.LayoutJson,
            "اسم تجريبي للاختبار — Sample Holder Name", null,
            "عنوان دورة تجريبي لمعاينة القالب", "Sample Course Title",
            "01/01/2026 – 15/01/2026", "12",
            "أ. المحاضر التجريبي", "Sample Instructor",
            "29/10/2026", "ZU-2026-GEN-0001",
            CertificateVerificationUrl.Build(verifyBase, "ZU-2026-GEN-0001"));
        byte[] qrPng = qr.GeneratePng(layer.VerifyUrl, 600);
        try { return Result<byte[]>.Success(renderer.Render(pdf, layer, qrPng)); }
        catch (Exception) { return Result<byte[]>.Failure("CERT_RENDERER_FAILED"); }
    }

    private static string? ValidateLayout(string layoutJson)
    {
        if (string.IsNullOrWhiteSpace(layoutJson)) return "CERT_LAYOUT_MISSING";
        JsonDocument document;
        try { document = JsonDocument.Parse(layoutJson); }
        catch (JsonException) { return "CERT_LAYOUT_INVALID_JSON"; }
        using (document)
        {
            if (!document.RootElement.TryGetProperty("fields", out var fields) ||
                fields.ValueKind != JsonValueKind.Object)
                return "CERT_LAYOUT_INVALID_JSON";
            foreach (string key in RequiredFields)
            {
                if (!fields.TryGetProperty(key, out var field) ||
                    field.ValueKind != JsonValueKind.Object)
                    return "CERT_LAYOUT_FIELD_MISSING";
                foreach (string coordinate in new[] { "x", "y" })
                    if (!field.TryGetProperty(coordinate, out _))
                        return "CERT_LAYOUT_FIELD_MISSING";
            }
            if (fields.TryGetProperty("qr", out var qr) &&
                (!qr.TryGetProperty("x", out _) || !qr.TryGetProperty("y", out _) ||
                 !qr.TryGetProperty("size", out _)))
                return "CERT_LAYOUT_FIELD_MISSING";
        }
        return null;
    }

    private async Task<byte[]> ReadStoredAsync(long fileId, CancellationToken ct)
    {
        var file = await db.StoredFiles.AsNoTracking()
            .Where(x => x.Id == fileId).Select(x => x.RelativePath)
            .SingleOrDefaultAsync(ct);
        if (file is null) throw new FileNotFoundException("Template file row missing.");
        await using var stream = await storage.OpenReadAsync(file, ct);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    /// <summary>Parses the first /MediaBox to obtain page size in PDF points.</summary>
    private static (decimal Width, decimal Height)? PdfPageSize(byte[] pdf)
    {
        if (pdf.Length < 8 || !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            return null;
        // A single-page template is required: more than one /Type /Page (excluding /Pages) is rejected.
        string text = System.Text.Encoding.Latin1.GetString(pdf);
        if (CountPages(text) > 1) return null;
        var match = System.Text.RegularExpressions.Regex.Match(text,
            @"/MediaBox\s*\[\s*(-?\d+(?:\.\d+)?)\s+(-?\d+(?:\.\d+)?)\s+(\d+(?:\.\d+)?)\s+(\d+(?:\.\d+)?)\s*]");
        if (!match.Success) return null;
        decimal x = decimal.Parse(match.Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        decimal y = decimal.Parse(match.Groups[2].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        decimal w = decimal.Parse(match.Groups[3].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        decimal h = decimal.Parse(match.Groups[4].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        decimal width = w - x, height = h - y;
        if (width <= 0 || height <= 0) return null;
        return (width, height);
    }

    private static int CountPages(string text)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf("/Type /Page", index, StringComparison.Ordinal)) >= 0)
        {
            // Skip "/Type /Pages" occurrences.
            if (index + 11 >= text.Length || text[index + 11] != 's') count++;
            index += 11;
        }
        return count;
    }
}

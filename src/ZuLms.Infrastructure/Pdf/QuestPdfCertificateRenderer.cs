using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZuLms.Application.Common.Abstractions;

namespace ZuLms.Infrastructure.Pdf;

/// <summary>
/// Renders the certificate by drawing positioned text fields (absolute coordinates from
/// layout_json, Tajawal font, field backgrounds and embedded QR) over the template PDF.
/// </summary>
public sealed class QuestPdfCertificateRenderer : ICertificatePdfRenderer
{
    static QuestPdfCertificateRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    private sealed record TextField(
        double X, double Y, double W, double H, string? Font, double Size,
        string? Weight, string Color, string Background, string Align, string? Dir,
        int MaxLines, bool ShrinkToFit);

    private sealed record QrField(double X, double Y, double Size);

    public byte[] Render(byte[] templatePdf, CertificateTextLayerInput input, byte[] qrPng)
    {
        ArgumentNullException.ThrowIfNull(templatePdf);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(qrPng);
        (var fields, var qr) = ParseLayout(input.LayoutJson);
        string? templatePath = null, textPath = null, outputPath = null;
        try
        {
            templatePath = Path.Combine(Path.GetTempPath(), $"zu-cert-{Guid.NewGuid():N}-template.pdf");
            textPath = Path.Combine(Path.GetTempPath(), $"zu-cert-{Guid.NewGuid():N}-text.pdf");
            outputPath = Path.Combine(Path.GetTempPath(), $"zu-cert-{Guid.NewGuid():N}-out.pdf");
            File.WriteAllBytes(templatePath, templatePdf);

            double width = (double)input.PageWidthPt, height = (double)input.PageHeightPt;
            if (width <= 0 || height <= 0) { width = 842; height = 595; }

            var values = new Dictionary<string, string>
            {
                ["holder_name"] = input.HolderNameAr,
                ["course_title"] = input.CourseTitleAr,
                ["course_period"] = input.CoursePeriodText,
                ["course_hours"] = input.CourseHoursText,
                ["instructor_name"] = input.InstructorNameAr,
                ["issue_date"] = input.IssueDateText,
                ["reference_no"] = input.ReferenceNo
            };

            Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size((float)width, (float)height, Unit.Point);
                    page.Margin(0);
                    page.PageColor(Colors.Transparent);
                    page.DefaultTextStyle(TextStyle.Default
                        .FontFamily("Tajawal").FontSize(20).FontColor("#022C5A"));
                    page.Content().Layers(layers =>
                    {
                        layers.PrimaryLayer();
                        foreach (var (key, field) in fields)
                        {
                            string value = values.GetValueOrDefault(key, string.Empty);
                            var box = layers.Layer()
                                .PaddingTop((float)field.Y, Unit.Point)
                                .PaddingLeft((float)field.X, Unit.Point);
                            if (field.W > 0) box = box.Width((float)field.W, Unit.Point);
                            if (field.H > 0) box = box.Height((float)field.H, Unit.Point);
                            if (!string.IsNullOrWhiteSpace(field.Background))
                                box = box.Background(field.Background);
                            box = IsLeftToRight(key, field)
                                ? box.ContentFromLeftToRight()
                                : box.ContentFromRightToLeft();
                            // Reduce the font before line clamping so long names use the
                            // permitted 60% size before they are shortened.
                            var text = box.Text(value);
                            text.FontSize((float)FitSize(value, field));
                            text.ClampLines(field.MaxLines);
                            if (field.Weight == "bold") text.Bold();
                            if (!string.IsNullOrEmpty(field.Font))
                                text.FontFamily(field.Font);
                            if (!string.IsNullOrEmpty(field.Color))
                                text.FontColor(field.Color);
                            if (field.Align == "center") text.AlignCenter();
                            else if (field.Align == "right" ||
                                     field.Dir == "rtl" && field.Align != "left")
                                text.AlignRight();
                            else text.AlignLeft();
                        }
                        if (qr is not null)
                            layers.Layer()
                                .PaddingTop((float)qr.Y, Unit.Point)
                                .PaddingLeft((float)qr.X, Unit.Point)
                                .Width((float)qr.Size, Unit.Point)
                                .Height((float)qr.Size, Unit.Point)
                                .Image(qrPng).FitArea();
                    });
                });
            }).GeneratePdf(textPath);

            DocumentOperation.LoadFile(templatePath)
                .OverlayFile(new DocumentOperation.LayerConfiguration { FilePath = textPath })
                .Save(outputPath);
            return File.ReadAllBytes(outputPath);
        }
        finally
        {
            TryDelete(templatePath);
            TryDelete(textPath);
            TryDelete(outputPath);
        }
    }

    private static double FitSize(string value, TextField field)
    {
        if (!field.ShrinkToFit || field.W <= 0 || value.Length == 0)
            return field.Size;
        double em = 0;
        foreach (char ch in value)
            em += char.IsWhiteSpace(ch) ? 0.3 :
                ch is >= '\u0600' and <= '\u06FF' ? 0.56 :
                char.IsUpper(ch) ? 0.66 : 0.53;
        double target = field.W * field.MaxLines / Math.Max(em, 1);
        return Math.Clamp(target, field.Size * 0.6, field.Size);
    }

    private static bool IsLeftToRight(string key, TextField field) =>
        field.Dir == "ltr" || key is "course_period" or "course_hours" or
            "issue_date" or "reference_no";

    private static (Dictionary<string, TextField> Fields, QrField? Qr) ParseLayout(string layoutJson)
    {
        var fields = new Dictionary<string, TextField>();
        QrField? qr = null;
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(layoutJson)
            ? "{}" : layoutJson);
        if (!document.RootElement.TryGetProperty("fields", out var root))
            return (fields, null);
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name == "qr")
            {
                qr = new QrField(
                    ReadDouble(property.Value, "x"), ReadDouble(property.Value, "y"),
                    Math.Max(20, ReadDouble(property.Value, "size")));
                continue;
            }
            fields[property.Name] = new TextField(
                ReadDouble(property.Value, "x"), ReadDouble(property.Value, "y"),
                ReadDouble(property.Value, "w"), ReadDouble(property.Value, "h"),
                ReadString(property.Value, "font"),
                Math.Max(6, ReadDouble(property.Value, "size", 20)),
                ReadString(property.Value, "weight"),
                ReadString(property.Value, "color") ?? "#022C5A",
                ReadString(property.Value, "background") ?? string.Empty,
                ReadString(property.Value, "align") ?? "center",
                ReadString(property.Value, "dir"),
                Math.Max(1, (int)ReadDouble(property.Value, "maxLines", 1)),
                ReadBool(property.Value, "shrinkToFit", true));
        }
        return (fields, qr);
    }

    private static double ReadDouble(JsonElement element, string name, double fallback = 0)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetDouble(out double result))
            return result;
        return fallback;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String)
            return value.GetString();
        return null;
    }

    private static bool ReadBool(JsonElement element, string name, bool fallback)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var value))
        {
            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;
        }
        return fallback;
    }

    private static void TryDelete(string? path)
    {
        if (path is null) return;
        try { File.Delete(path); } catch { /* best effort */ }
    }
}

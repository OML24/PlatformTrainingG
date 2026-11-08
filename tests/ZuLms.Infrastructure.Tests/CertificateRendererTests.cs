using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Infrastructure.Pdf;
using ZuLms.Infrastructure.Qr;

namespace ZuLms.Infrastructure.Tests;

public sealed class CertificateRendererTests
{
    [Fact]
    public void RendersArabicTextAndQrOverSinglePageTemplate()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var assembly = typeof(QuestPdfCertificateRenderer).Assembly;
        foreach (string name in assembly.GetManifestResourceNames()
            .Where(x => x.Contains("Tajawal", StringComparison.OrdinalIgnoreCase)))
        {
            using var font = assembly.GetManifestResourceStream(name);
            Assert.NotNull(font);
            FontManager.RegisterFontFromStream(font);
        }
        var template = Document.Create(document => document.Page(page =>
        {
            page.Size(842, 595, Unit.Point);
            page.Margin(0);
            page.Content().Text("ZU template");
        })).GeneratePdf();
        const string layout = """
            {"fields":{
              "holder_name":{"x":120,"y":120,"w":400,"h":50,"size":28,"font":"Tajawal","maxLines":1,"shrinkToFit":true},
              "course_title":{"x":120,"y":200,"w":400,"h":80,"size":25,"font":"Tajawal","maxLines":2,"shrinkToFit":true},
              "qr":{"x":650,"y":400,"size":100}
            }}
            """;
        var input = new CertificateTextLayerInput(842, 595, layout,
            "اسم عربي طويل لاختبار تصغير النص داخل مساحة الشهادة", null,
            "دورة تدريبية في إدارة التعلم الإلكتروني", null,
            "01/01/2026 – 15/01/2026", "12", "المحاضر", null,
            "29/10/2026", "ZU-2026-GEN-0001",
            "https://portal.zu.edu.jo/verify?cert=ZU-2026-GEN-0001");
        var qr = new QrCodeService().GeneratePng(input.VerifyUrl, 600);

        byte[] pdf = new QuestPdfCertificateRenderer().Render(template, input, qr);
        string? previewPath = Environment.GetEnvironmentVariable("ZU_CERT_PREVIEW_PATH");
        if (!string.IsNullOrWhiteSpace(previewPath))
            File.WriteAllBytes(previewPath, pdf);

        Assert.True(pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
        Assert.True(pdf.Length > template.Length);
    }
}

using QRCoder;
using ZuLms.Application.Common.Abstractions;

namespace ZuLms.Infrastructure.Qr;

public sealed class QrCodeService : IQrCodeService
{
    public byte[] GeneratePng(string content, int targetPixels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        // Aim for ~targetPixels while keeping a whole number of pixels per module.
        int pixelsPerModule = Math.Max(2, targetPixels /
            Math.Max(1, data.ModuleMatrix.Count));
        using var code = new PngByteQRCode(data);
        return code.GetGraphic(pixelsPerModule);
    }
}

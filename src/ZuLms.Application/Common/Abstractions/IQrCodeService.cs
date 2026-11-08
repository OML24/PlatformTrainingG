namespace ZuLms.Application.Common.Abstractions;

/// <summary>Generates QR PNG images (error correction level M).</summary>
public interface IQrCodeService
{
    /// <summary>Returns PNG bytes of roughly the requested pixel size.</summary>
    byte[] GeneratePng(string content, int targetPixels);
}

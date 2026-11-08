using ZuLms.Application.Common.Abstractions;

namespace ZuLms.Application.Features.Certificates;

public static class CertificateVerificationUrl
{
    public static async Task<string> BaseAsync(ISettingsService settings,
        CancellationToken ct)
    {
        string? publicBase = Environment.GetEnvironmentVariable("ZU_PUBLIC_BASE_URL");
        if (!string.IsNullOrWhiteSpace(publicBase))
            return $"{publicBase.Trim().TrimEnd('/')}/verify";
        return (await settings.GetAsync<string>("cert.verify_base_url", ct)).TrimEnd('/');
    }

    public static string Build(string verifyBase, string referenceNo) =>
        $"{verifyBase.TrimEnd('/')}?cert={Uri.EscapeDataString(referenceNo)}";
}

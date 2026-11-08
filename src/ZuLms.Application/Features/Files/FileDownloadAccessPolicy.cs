using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Security;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Files;

public static class FileDownloadAccessPolicy
{
    public static bool AllowsPersonalDownload(FileKind kind, EffectivePermissionSet permissions) => kind switch
    {
        FileKind.ProfessionalCertificate =>
            ScopeGuard.AllowsPersonalRead(permissions, "procert.download.own"),
        FileKind.ImpactFile => ScopeGuard.AllowsPersonalRead(permissions, "impact.submit"),
        FileKind.CertificatePdf => ScopeGuard.AllowsPersonalRead(permissions, "certificates.download.own"),
        _ => false
    };

    public static bool AllowsProCertReviewerDownload(EffectivePermissionSet permissions) =>
        (permissions.HasMode(RoleCode.Admin) || permissions.HasMode(RoleCode.Quality)) &&
        permissions.Keys.Contains("procert.download.any");

    public static bool AllowsVaultDownload(EffectivePermissionSet permissions, FileKind kind) =>
        kind != FileKind.ProfessionalCertificate &&
        (permissions.HasMode(RoleCode.Admin) || permissions.HasMode(RoleCode.Quality)) &&
        permissions.Keys.Contains("vault.download");
}

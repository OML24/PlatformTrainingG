using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Features.Files;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Tests;

public sealed class FileDownloadAccessPolicyTests
{
    [Fact]
    public void PersonalFileKindsRequireMemberModeAndTheirOwnDownloadKey()
    {
        var member = Set(RoleCode.Member, RoleCode.Member,
            "procert.download.own", "impact.submit", "certificates.download.own");
        Assert.True(FileDownloadAccessPolicy.AllowsPersonalDownload(
            FileKind.ProfessionalCertificate, member));
        Assert.True(FileDownloadAccessPolicy.AllowsPersonalDownload(FileKind.ImpactFile, member));
        Assert.True(FileDownloadAccessPolicy.AllowsPersonalDownload(FileKind.CertificatePdf, member));

        var quality = Set(RoleCode.Quality, RoleCode.Quality, "procert.download.own",
            "impact.submit", "certificates.download.own", "procert.download.any",
            "vault.download");
        Assert.False(FileDownloadAccessPolicy.AllowsPersonalDownload(
            FileKind.ProfessionalCertificate, quality));
        Assert.False(FileDownloadAccessPolicy.AllowsPersonalDownload(FileKind.ImpactFile, quality));
        Assert.False(FileDownloadAccessPolicy.AllowsPersonalDownload(FileKind.CertificatePdf, quality));
        Assert.True(FileDownloadAccessPolicy.AllowsProCertReviewerDownload(quality));
    }

    [Fact]
    public void ProCertReviewerPermissionDoesNotBecomeGenericVaultPermission()
    {
        var reviewer = Set(RoleCode.Admin, RoleCode.Admin, "procert.download.any");
        Assert.True(FileDownloadAccessPolicy.AllowsProCertReviewerDownload(reviewer));
        Assert.False(FileDownloadAccessPolicy.AllowsVaultDownload(
            reviewer, FileKind.ProfessionalCertificate));
        Assert.False(FileDownloadAccessPolicy.AllowsVaultDownload(
            reviewer, FileKind.QualityMinutes));
    }

    private static EffectivePermissionSet Set(RoleCode activeMode,
        RoleCode role, params string[] keys) => new(7, activeMode,
            new HashSet<RoleCode> { role }, new HashSet<string>(keys, StringComparer.Ordinal),
            new HashSet<long>());
}

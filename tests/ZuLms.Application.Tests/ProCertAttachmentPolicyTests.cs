using ZuLms.Application.Features.ProCerts;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Tests;

public sealed class ProCertAttachmentPolicyTests
{
    [Fact]
    public void ForeignReplacementIsRejectedButCurrentLegacyAttachmentCanRemain()
    {
        Assert.False(ProCertAttachmentPolicy.Allows(202, 101, true,
            FileKind.ProfessionalCertificate, 99, actorUserId: 7));

        Assert.True(ProCertAttachmentPolicy.Allows(101, 101, true,
            FileKind.Other, uploadedBy: 99, actorUserId: 7));

        Assert.True(ProCertAttachmentPolicy.Allows(202, null, true,
            FileKind.ProfessionalCertificate, uploadedBy: 7, actorUserId: 7));
        Assert.False(ProCertAttachmentPolicy.Allows(202, null, true,
            FileKind.ProfessionalCertificate, uploadedBy: 99, actorUserId: 7));
        Assert.False(ProCertAttachmentPolicy.Allows(202, null, true,
            FileKind.ImpactFile, uploadedBy: 7, actorUserId: 7));
        Assert.False(ProCertAttachmentPolicy.Allows(202, 101, false,
            FileKind.ProfessionalCertificate, uploadedBy: 7, actorUserId: 7));
    }
}

using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.ProCerts;

public static class ProCertAttachmentPolicy
{
    public static bool Allows(long requestedFileId, long? currentFileId,
        bool fileExistsAndActive, FileKind? fileKind, long? uploadedBy,
        long actorUserId) =>
        fileExistsAndActive &&
        (currentFileId == requestedFileId ||
         fileKind == FileKind.ProfessionalCertificate && uploadedBy == actorUserId);
}

namespace ZuLms.Domain.Enums;

public enum SyncReviewReason
{
    MissingFromSource,
    UnresolvedReference,
    DuplicateLoginName,
    DuplicateSourceKey,
    SharedEmail,
    StatusChanged
}


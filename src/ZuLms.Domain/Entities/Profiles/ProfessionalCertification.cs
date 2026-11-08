using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Profiles;

public sealed class ProfessionalCertification : BaseEntity, ISoftDeletable
{
    public long UserId { get; set; }
    public long CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string IssuerName { get; set; } = string.Empty;
    public string? IssuerWebsiteUrl { get; set; }
    public string? Description { get; set; }
    public string? CredentialCode { get; set; }
    public string? VerifyUrl { get; set; }
    public DateOnly IssuedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public long FileId { get; set; }
    public ProCertStatus Status { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public long? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionNotes { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public long? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
    public User? User { get; set; }
    public ProfessionalCertCategory? Category { get; set; }
    public StoredFile? File { get; set; }
    public User? DecidedByUser { get; set; }
}


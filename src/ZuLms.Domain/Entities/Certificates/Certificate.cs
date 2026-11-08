using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Certificates;

public sealed class Certificate : BaseEntity, ISoftDeletable
{
    public string ReferenceNo { get; set; } = string.Empty;
    public string VerificationToken { get; set; } = string.Empty;
    public long CourseId { get; set; }
    public long ParticipantId { get; set; }
    public long UserId { get; set; }
    public CertificateStatus Status { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public long TemplateId { get; set; }
    public long PdfFileId { get; set; }
    public int PdfRenderVersion { get; set; } = 1;
    public string HolderNameAr { get; set; } = string.Empty;
    public string? HolderNameEn { get; set; }
    public string CourseTitleAr { get; set; } = string.Empty;
    public string? CourseTitleEn { get; set; }
    public string CoursePeriodText { get; set; } = string.Empty;
    public decimal CourseHours { get; set; }
    public string InstructorNameAr { get; set; } = string.Empty;
    public string? InstructorNameEn { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public CertificateRevokeReason? RevokedReason { get; set; }
    public long? RevokedBy { get; set; }
    public string? RevokedNote { get; set; }
    public bool HiddenByCourseDisable { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public long? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
    public Course? Course { get; set; }
    public CourseParticipant? Participant { get; set; }
    public User? User { get; set; }
    public CertificateTemplate? Template { get; set; }
    public StoredFile? PdfFile { get; set; }
}

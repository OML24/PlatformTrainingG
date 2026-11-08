using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Training;

public sealed class ExternalInstructorAccountRequest : BaseEntity
{
    public long CourseId { get; set; }
    public long RequestedByUserId { get; set; }
    public string FullNameAr { get; set; } = string.Empty;
    public string? FullNameEn { get; set; }
    public string OrganizationAr { get; set; } = string.Empty;
    public string? OrganizationEn { get; set; }
    public string Email { get; set; } = string.Empty;
    public ExternalAccountRequestStatus Status { get; set; }
    public long? ForwardedBy { get; set; }
    public DateTimeOffset? ForwardedAt { get; set; }
    public long? HandledBy { get; set; }
    public DateTimeOffset? HandledAt { get; set; }
    public long? CreatedUserId { get; set; }
    public string? RejectReason { get; set; }
    public Course? Course { get; set; }
    public User? RequestedByUser { get; set; }
    public User? ForwardedByUser { get; set; }
    public User? HandledByUser { get; set; }
    public User? CreatedUser { get; set; }
}


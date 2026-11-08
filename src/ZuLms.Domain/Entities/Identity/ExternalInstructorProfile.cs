using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Identity;

public sealed class ExternalInstructorProfile
{
    public long UserId { get; set; }
    public string OrganizationAr { get; set; } = string.Empty;
    public string? OrganizationEn { get; set; }
    public string? Phone { get; set; }
    public long? CvFileId { get; set; }
    public string? Notes { get; set; }
    public long? CreatedFromRequestId { get; set; }
    public User? User { get; set; }
    public StoredFile? CvFile { get; set; }
    public ExternalInstructorAccountRequest? CreatedFromRequest { get; set; }
}


using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Profiles;

public sealed class MemberProfile
{
    public long UserId { get; set; }
    public string? BioAr { get; set; }
    public string? BioEn { get; set; }
    public long? PhotoFileId { get; set; }
    public string[] ExpertiseAr { get; set; } = [];
    public string[] ExpertiseEn { get; set; } = [];
    public string? ContactMethod { get; set; }
    public string? ContactValue { get; set; }
    public bool ShowInDirectory { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public User? User { get; set; }
    public StoredFile? PhotoFile { get; set; }
}


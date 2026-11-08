using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Profiles;

public sealed class ProfessionalCertEvent
{
    public long Id { get; set; }
    public long CertId { get; set; }
    public string Event { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public long ByUserId { get; set; }
    public DateTimeOffset At { get; set; }
    public ProfessionalCertification? Cert { get; set; }
    public User? ByUser { get; set; }
}


using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Certificates;

public sealed class CertificateTemplate : BaseEntity
{
    public int VersionNo { get; set; }
    public long FileId { get; set; }
    public decimal PageWidthPt { get; set; }
    public decimal PageHeightPt { get; set; }
    public string LayoutJson { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public string? Notes { get; set; }
    public StoredFile? File { get; set; }
}


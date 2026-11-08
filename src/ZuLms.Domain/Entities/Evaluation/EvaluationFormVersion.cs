using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Evaluation;

public sealed class EvaluationFormVersion : BaseEntity
{
    public int VersionNo { get; set; }
    public FormVersionStatus Status { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public long? PublishedBy { get; set; }
    public User? PublishedByUser { get; set; }
}


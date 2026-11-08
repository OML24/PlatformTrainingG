using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Evaluation;

public sealed class EvaluationQuestion
{
    public long Id { get; set; }
    public long VersionId { get; set; }
    public Guid QuestionKey { get; set; }
    public int SortOrder { get; set; }
    public string TextAr { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public EvaluationFormVersion? Version { get; set; }
}


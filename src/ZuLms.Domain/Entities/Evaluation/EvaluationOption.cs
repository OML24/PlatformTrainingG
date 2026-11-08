using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Evaluation;

public sealed class EvaluationOption
{
    public long Id { get; set; }
    public long QuestionId { get; set; }
    public int SortOrder { get; set; }
    public string LabelAr { get; set; } = string.Empty;
    public string? LabelEn { get; set; }
    public short? Score { get; set; }
    public EvaluationQuestion? Question { get; set; }
}


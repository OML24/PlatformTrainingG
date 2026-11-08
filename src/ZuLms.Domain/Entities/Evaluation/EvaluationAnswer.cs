using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Evaluation;

public sealed class EvaluationAnswer
{
    public long Id { get; set; }
    public long ResponseId { get; set; }
    public long QuestionId { get; set; }
    public long OptionId { get; set; }
    public EvaluationResponse? Response { get; set; }
    public EvaluationQuestion? Question { get; set; }
    public EvaluationOption? Option { get; set; }
}


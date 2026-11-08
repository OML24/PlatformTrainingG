using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Courses;

public interface IEvaluationService
{
    Task<Result<EvaluationFormDto>> GetFormAsync(long actorId,
        long courseId, CancellationToken ct);
    Task<Result> SubmitAsync(long actorId, long courseId,
        IReadOnlyList<EvaluationAnswerInput> answers, CancellationToken ct);
}

public sealed record EvaluationFormDto(long VersionId, string CourseTitleAr,
    string InstructorNameAr, DateOnly EndDate,
    IReadOnlyList<EvaluationQuestionDto> Questions);
public sealed record EvaluationQuestionDto(long Id, string TextAr,
    IReadOnlyList<EvaluationOptionDto> Options);
public sealed record EvaluationOptionDto(long Id, string LabelAr);
public sealed record EvaluationAnswerInput(long QuestionId, long OptionId);

using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Courses;

public interface IEvaluationFormAdminService
{
    Task<Result<EvaluationDraftDto>> OpenDraftAsync(long actorId,
        CancellationToken ct);
    Task<Result> SaveDraftAsync(long actorId, long draftId,
        EvaluationDraftInput input, CancellationToken ct);
    Task<Result<int>> PublishAsync(long actorId, long draftId,
        CancellationToken ct);
}

public sealed record EvaluationDraftDto(long Id, int VersionNo,
    string TitleAr, IReadOnlyList<EvaluationDraftQuestionDto> Questions);
public sealed record EvaluationDraftQuestionDto(long Id, Guid QuestionKey,
    string TextAr, IReadOnlyList<EvaluationDraftOptionDto> Options);
public sealed record EvaluationDraftOptionDto(long Id, string LabelAr,
    short? Score);
public sealed record EvaluationDraftInput(string TitleAr,
    IReadOnlyList<EvaluationDraftQuestionInput> Questions);
public sealed record EvaluationDraftQuestionInput(long? ExistingId,
    bool KeepQuestionKey, string TextAr,
    IReadOnlyList<EvaluationDraftOptionInput> Options);
public sealed record EvaluationDraftOptionInput(string LabelAr, short? Score);

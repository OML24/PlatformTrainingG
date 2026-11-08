using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface IExamService
{
    Task<Result<ExamDto?>> GetAsync(long actorId, long courseId, CancellationToken ct);
    Task<Result<long>> CreateAsync(long actorId, long courseId, string nameAr,
        CancellationToken ct);
    Task<Result> RecordAsync(long actorId, long courseId, long participantId,
        ExamOutcome outcome, CancellationToken ct);
}

public sealed record ExamDto(long Id, string NameAr,
    IReadOnlyList<ExamParticipantDto> Participants);
public sealed record ExamParticipantDto(long ParticipantId, string NameAr,
    string LoginName, ExamOutcome? Outcome);

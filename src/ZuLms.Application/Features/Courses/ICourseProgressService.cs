using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface ICourseProgressService
{
    Task<Result<CourseProgressDto>> GetMineAsync(long actorId, long courseId,
        CancellationToken ct);
    Task<Result<IReadOnlyList<CourseParticipantProgressDto>>> GetRosterAsync(
        long actorId, long courseId, CancellationToken ct);
}

public sealed record CourseProgressDto(int AttendedLectures,
    int TotalLectures, int RequiredLectures, int ThresholdPercent,
    bool IsFinal, bool AttendanceOk, bool HasExam, ExamOutcome? ExamOutcome,
    bool ImpactOpen,
    bool SurveyCompleted, bool ImpactApproved, bool Eligible,
    IReadOnlyList<string> UnmetReasons, bool? HistoricalPassed);

public sealed record CourseParticipantProgressDto(long ParticipantId,
    string NameAr, string LoginName, CourseProgressDto Progress,
    string? CertificateReference, CertificateStatus? CertificateStatus);

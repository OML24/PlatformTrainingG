using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface ILectureService
{
    Task<Result<long>> CreateAsync(long actorId, long courseId,
        LectureInput input, CancellationToken ct);
    Task<Result<IReadOnlyList<LectureDto>>> ListAsync(long actorId,
        long courseId, CancellationToken ct);
    Task<Result<ActivatedAttendanceCodeDto>> ActivateCodeAsync(long actorId,
        long lectureId, CancellationToken ct);
    Task<Result> UpdateAsync(long actorId, long lectureId,
        LectureScheduleInput input, CancellationToken ct);
}

public sealed record LectureInput(string? TitleAr, string? TitleEn,
    LectureMode Mode, DateTime StartsAt, DateTime EndsAt,
    string? LocationAr, string? LocationEn, string? TeamsUrl,
    string? NotesAr);
public sealed record LectureDto(long Id, string? TitleAr, LectureMode Mode,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? LocationAr,
    string? TeamsUrl, bool IsActive, bool CodeIsActive, bool IsPresent);
public sealed record ActivatedAttendanceCodeDto(long LectureId,
    string Code, DateTimeOffset ExpiresAt);
public sealed record LectureScheduleInput(DateTime StartsAt, DateTime EndsAt,
    string? LocationAr, string? LocationEn, string? TeamsUrl);

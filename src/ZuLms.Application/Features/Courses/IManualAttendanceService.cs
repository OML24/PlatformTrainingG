using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Courses;

public interface IManualAttendanceService
{
    Task<Result<IReadOnlyList<AttendanceRosterRowDto>>> RosterAsync(
        long actorId, long lectureId, CancellationToken ct);
    Task<Result> SetAsync(long actorId, long lectureId,
        long participantId, bool isPresent, string? reason, CancellationToken ct);
}

public sealed record AttendanceRosterRowDto(long ParticipantId,
    long UserId, string NameAr, string LoginName, bool IsPresent);

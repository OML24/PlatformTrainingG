using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Courses;

public interface IManualParticipantService
{
    Task<Result<ManualParticipantPageDto>> ListAsync(long actorUserId,
        long courseId, CancellationToken ct);
    Task<Result<IReadOnlyList<InstructorOptionDto>>> SearchEmployeesAsync(
        long actorUserId, string? search, CancellationToken ct);
    Task<Result> AddAsync(long actorUserId, long courseId,
        IReadOnlyList<long> userIds, bool confirmCapacityOverride, CancellationToken ct);
    Task<Result> RemoveAsync(long actorUserId, long courseId, long userId,
        string reason, bool confirmCertificateRevocation, CancellationToken ct);
}

public sealed record ManualParticipantRowDto(long UserId, string NameAr,
    string LoginName, string Source, DateTimeOffset JoinedAt,
    bool HasIssuedCertificate, int? EmpStatus = null,
    string? EmpStatusDesc = null);
public sealed record ManualParticipantPageDto(long CourseId, string CourseNo,
    string TitleAr, int? MaxParticipants,
    IReadOnlyList<ManualParticipantRowDto> Participants);

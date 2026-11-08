using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface ICourseProposalQueryService
{
    Task<Result<CourseProposalPageDto>> ListMineAsync(long actorUserId, int page,
        CancellationToken ct);
    Task<Result<CourseProposalPageDto>> ListPendingAsync(long actorUserId, int page,
        CancellationToken ct);
    Task<Result<CourseProposalDetailsDto>> GetAsync(long actorUserId, long courseId,
        CancellationToken ct);
    Task<Result<DeanProposalLookupsDto>> DeanLookupsAsync(long actorUserId,
        CancellationToken ct);
    Task<Result<IReadOnlyList<InstructorOptionDto>>> SearchFacultyMembersAsync(
        long actorUserId, long centerId, string? search, CancellationToken ct);
    Task<Result<IReadOnlyList<InstructorOptionDto>>> SearchExternalInstructorsAsync(
        long actorUserId, string? search, CancellationToken ct);
    Task<Result<IReadOnlyList<DeanCenterOptionDto>>> ListFacultiesAsync(
        long actorUserId, CancellationToken ct);
}

public sealed record CourseProposalRowDto(long Id, uint Version, string CourseNo,
    string TitleAr, string CenterNameAr, CourseStatus Status, int CurrentRevisionNo,
    DateTimeOffset StatusChangedAt, string? LastReturnNotes, long ProposingCenterId);
public sealed record CourseProposalPageDto(int Page, int PageSize, int Total,
    IReadOnlyList<CourseProposalRowDto> Items);
public sealed record CourseProposalRevisionDto(int RevisionNo, DateTimeOffset SubmittedAt,
    ProposalDecision Decision, string? DecisionNotes, CourseProposalInput Input);
public sealed record CourseProposalDetailsDto(CourseProposalRowDto Course,
    IReadOnlyList<CourseProposalRevisionDto> Revisions,
    DateOnly? EvaluationEndDate, AudienceType AudienceType, long? TargetCenterId,
    DateTimeOffset? RegistrationDeadline, int? MaxParticipants, bool HasExam,
    InstructorKind InstructorKind, long? InstructorUserId, bool ManagedByQuality,
    string? InstructorLoginName, DateOnly SuggestedEvaluationEndDate);
public sealed record DeanCenterOptionDto(long Id, string NameAr);
public sealed record TrackOptionDto(long Id, string NameAr);
public sealed record DeanProposalLookupsDto(IReadOnlyList<DeanCenterOptionDto> Centers,
    IReadOnlyList<TrackOptionDto> Tracks);
public sealed record InstructorOptionDto(long UserId, string NameAr, string LoginName,
    string? NameEn = null, string? DepartmentNameAr = null,
    int? EmpStatus = null, string? EmpStatusDesc = null);

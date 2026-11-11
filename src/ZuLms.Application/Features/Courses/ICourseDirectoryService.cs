using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface ICourseDirectoryService
{
    Task<Result<CourseDirectoryLookupsDto>> LookupsAsync(long actorUserId,
        CourseDirectoryView view, CancellationToken ct);
    Task<Result<CourseDirectoryPageDto>> ListAsync(long actorUserId,
        CourseDirectoryFilter filter, CancellationToken ct);
    Task<Result<CourseDirectoryDetailsDto>> GetAsync(long actorUserId,
        long courseId, CourseDirectoryView view, CancellationToken ct);
}

public sealed record CourseDirectoryLookupsDto(
    IReadOnlyList<DeanCenterOptionDto> Faculties,
    IReadOnlyList<TrackOptionDto> Tracks);

public enum CourseDirectoryView { Auto, All, Dean, Instructor, Member, Available }

public sealed record CourseDirectoryFilter(
    CourseDirectoryView View, string? Search, CourseStatus? Status,
    long? CenterId, long? TrackId, DeliveryMode? DeliveryMode,
    DateOnly? FromDate, DateOnly? ToDate, bool? IsActive,
    bool? IsArchived, int Page = 1);

public sealed record CourseDirectoryRowDto(long Id, string CourseNo,
    string TitleAr, string? TitleEn, CourseStatus Status,
    CourseOrigin Origin, string? CenterNameAr, string TrackNameAr,
    string InstructorNameAr, DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate, bool IsHistorical, bool IsActive,
    bool IsArchived, int ParticipantsCount, uint Version);

public sealed record CourseDirectoryPageDto(int Page, int PageSize,
    int Total, IReadOnlyList<CourseDirectoryRowDto> Items);

public sealed record CourseDirectoryDetailsDto(CourseDirectoryRowDto Course,
    string ObjectivesAr, string? ObjectivesEn,
    string TargetAudienceAr, string? TargetAudienceEn,
    decimal TotalHours, DeliveryMode DeliveryMode,
    AudienceType AudienceType, string? TargetCenterNameAr,
    DateTimeOffset? RegistrationDeadline, int? MaxParticipants,
    DateOnly? EvaluationEndDate, bool HasExam, bool ManagedByQuality,
    string? CancellationReason, string? HistoricalNotes,
    IReadOnlyList<CourseHistoryDto> History,
    IReadOnlyList<CourseFileDto> Files, bool CanAdminActions,
    bool CanEditExamRequirement, bool CanEditCourse, bool CanForceStatus,
    long TrackId, long? TargetCenterId,
    short? AttendanceThresholdPercentOverride, bool CanExtendEvaluation,
    InstructorKind InstructorKind, long? InstructorUserId,
    string? InstructorNameEn, string? InstructorOrgAr,
    string? InstructorOrgEn, bool CanEditInstructor);

public sealed record CourseHistoryDto(CourseStatus? FromStatus,
    CourseStatus ToStatus, DateTimeOffset ChangedAt, string? Reason);
public sealed record CourseFileDto(long FileId, string Purpose,
    string Name, string MimeType);

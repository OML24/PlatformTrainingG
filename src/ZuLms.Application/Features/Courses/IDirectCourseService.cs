using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface IDirectCourseService
{
    Task<Result<long>> CreateAsync(long actorUserId, DirectCourseInput input,
        CancellationToken ct);
    Task<Result<DirectCourseLookupsDto>> LookupsAsync(long actorUserId,
        CancellationToken ct);
    Task<Result<IReadOnlyList<InstructorOptionDto>>> SearchInstructorsAsync(
        long actorUserId, bool external, string? search, CancellationToken ct);
    Task<Result<IReadOnlyList<HistoricalEmployeeDto>>> HistoricalEmployeesAsync(
        long actorUserId, long centerId, CancellationToken ct);
}

public sealed record DirectCourseLookupsDto(
    IReadOnlyList<DeanCenterOptionDto> Faculties,
    IReadOnlyList<TrackOptionDto> Tracks, bool CanCreateHistorical);

public enum DirectInstructorChoice
{
    Internal,
    ExistingExternal,
    NewExternalAccount,
    ManagedByQuality
}

public sealed record DirectCourseInput(
    string TitleAr, string? TitleEn, long TrackId, DeliveryMode DeliveryMode,
    string ObjectivesAr, string? ObjectivesEn, string TargetAudienceAr,
    string? TargetAudienceEn, decimal TotalHours, DateOnly PlannedStartDate,
    DateOnly PlannedEndDate, AudienceType AudienceType, long? TargetCenterId,
    long? RelatedCenterId, DateTimeOffset? RegistrationDeadline,
    int? MaxParticipants, bool HasExam, DateOnly? EvaluationEndDate,
    DirectInstructorChoice InstructorChoice, long? InstructorUserId,
    string? ExternalNameAr, string? ExternalNameEn, string? ExternalOrgAr,
    string? ExternalOrgEn, string? ExternalEmail, long? CouncilLetterFileId,
    long? InstructorCvFileId, bool IsHistorical, string? HistoricalNotes,
    DateOnly? ImpactEndDate = null,
    IReadOnlyList<HistoricalParticipantInput>? HistoricalParticipants = null,
    bool HistoricalEvaluationRequired = false);

public sealed record HistoricalParticipantInput(long UserId, bool Passed);
public sealed record HistoricalEmployeeDto(long UserId, string NameAr,
    string LoginName, int? EmpStatus = null, string? EmpStatusDesc = null);

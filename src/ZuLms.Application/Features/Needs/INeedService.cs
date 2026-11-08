using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Needs;

public interface INeedService
{
    Task<Result<NeedLookupsDto>> LookupsAsync(long actorUserId, CancellationToken ct);
    Task<Result<IReadOnlyList<NeedInstructorOptionDto>>> SearchInstructorsAsync(
        long actorUserId, long facultyId, string? search, CancellationToken ct);
    Task<Result<long>> CreateAsync(long actorUserId, NeedInput input,
        CancellationToken ct);
    Task<Result> ResubmitAsync(long actorUserId, long needId, NeedInput input,
        CancellationToken ct);
    Task<Result> DecideAsync(long actorUserId, long needId, bool approve,
        string? notes, CancellationToken ct);
    Task<Result<NeedPageDto>> ListAsync(long actorUserId, string? search,
        NeedStatus? status, int page, CancellationToken ct);
    Task<Result<NeedDetailsDto>> GetAsync(long actorUserId, long needId,
        CancellationToken ct);
    Task<Result> DeactivateAsync(long actorUserId, long needId, string reason,
        CancellationToken ct);
}

public sealed record NeedOptionDto(long Id, string NameAr, string? NameEn);
public sealed record NeedLookupsDto(IReadOnlyList<NeedOptionDto> Tracks,
    IReadOnlyList<NeedOptionDto> OtherFaculties);
public sealed record NeedInstructorOptionDto(long UserId, string NameAr, string? NameEn,
    string LoginName, long FacultyId, string FacultyNameAr, string? FacultyNameEn);

public sealed record NeedInput(
    string TitleAr, string? TitleEn, string DescriptionAr, string? DescriptionEn,
    long? TrackId, int? ExpectedParticipants, string? PreferredTimeframeAr,
    long? RequestedInstructorUserId, string? RequestedInstructorNoteAr,
    long LetterFileId);

public sealed record NeedRowDto(long Id, string TitleAr, string CenterNameAr,
    string DeanNameAr, NeedStatus Status, int CurrentRevision,
    DateTimeOffset CreatedAt, long LetterFileId);

public sealed record NeedPageDto(int Page, int PageSize, int Total,
    IReadOnlyList<NeedRowDto> Items);

public sealed record NeedEventDto(string Event, string? Notes, string ByNameAr,
    DateTimeOffset At);

public sealed record NeedDetailsDto(long Id, string TitleAr, string? TitleEn,
    string DescriptionAr, string? DescriptionEn, long? TrackId, string? TrackNameAr,
    int? ExpectedParticipants, string? PreferredTimeframeAr,
    long? RequestedInstructorUserId, long? RequestedInstructorCenterId,
    string? RequestedInstructorNameAr,
    string? RequestedInstructorNoteAr, long LetterFileId, string CenterNameAr,
    string DeanNameAr, NeedStatus Status, int CurrentRevision,
    string? LastReturnNotes, DateTimeOffset CreatedAt,
    IReadOnlyList<NeedEventDto> Events);

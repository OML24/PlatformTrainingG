using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface IExternalInstructorAccountService
{
    Task<Result<ExternalRequestPageDto>> ListPendingAsync(long actorUserId, int page,
        CancellationToken ct);
    Task<Result<string>> SuggestLoginAsync(long actorUserId, CancellationToken ct);
    Task<Result<ExternalAccountCreatedDto>> CompleteAsync(long actorUserId,
        long requestId, string loginName, string? password, CancellationToken ct);
}

public sealed record ExternalRequestDto(long Id, long CourseId, string CourseNo,
    string FullNameAr, string OrganizationAr, string Email,
    ExternalAccountRequestStatus Status, string CourseTitleAr, CourseStatus CourseStatus,
    DateTimeOffset RequestedAt);
public sealed record ExternalRequestPageDto(int Page, int PageSize, int Total,
    IReadOnlyList<ExternalRequestDto> Items);
public sealed record ExternalAccountCreatedDto(long UserId, string LoginName);

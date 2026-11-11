using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Courses;

public interface ISelfRegistrationService
{
    Task<Result<AvailableCoursePageDto>> ListAvailableAsync(long userId,
        int page, CancellationToken ct);
    Task<Result> RegisterAsync(long userId, long courseId, CancellationToken ct);
}

public sealed record AvailableCourseDto(long Id, string CourseNo, string TitleAr,
    string? TitleEn, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate,
    int? MaxParticipants, DateTimeOffset? RegistrationDeadline, bool IsRegistered);
public sealed record AvailableCoursePageDto(int Page, int PageSize, int Total,
    IReadOnlyList<AvailableCourseDto> Items);

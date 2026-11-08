using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Stats;

public interface IStatsService
{
    Task<Result<StatsLookupsDto>> LookupsAsync(long actorUserId, RoleCode contextRole,
        string? userSearch, CancellationToken ct);
    Task<Result<DashboardStatsDto>> DashboardAsync(long actorUserId,
        StatsFilter filter, CancellationToken ct);
}

public sealed record StatsFilter(RoleCode ContextRole, long? CenterId, long? UserId,
    DateOnly? FromDate, DateOnly? ToDate);

public sealed record StatsOptionDto(long Id, string Name);
public sealed record StatsLookupsDto(bool CanFilterFaculty, bool CanFilterUser,
    IReadOnlyList<StatsOptionDto> Faculties, IReadOnlyList<StatsOptionDto> Users);
public sealed record StatsSeriesDto(string Label, int Value);
public sealed record StatsMonthlyDto(string Label, int Courses, int Participants);
public sealed record StatsPersonDto(long Id, string Name, int Courses,
    int FinishedCourses, int Certificates);

public sealed record DashboardStatsDto(
    string ScopeLabel,
    int Courses,
    int Participants,
    int Certificates,
    int FinishedCourses,
    decimal CompletionRate,
    IReadOnlyList<StatsSeriesDto> ByStatus,
    IReadOnlyList<StatsSeriesDto> ByFaculty,
    IReadOnlyList<StatsSeriesDto> ByTrack,
    IReadOnlyList<StatsMonthlyDto> Monthly,
    StatsPersonDto? Person);

using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Stats;

public sealed class StatsService(IAppDbContext db, IPermissionService permissions)
    : IStatsService
{
    private static readonly (CourseStatus Status, string Label)[] StatusLabels =
    [
        (CourseStatus.Proposed, "مقترحة"), (CourseStatus.Returned, "معادة"),
        (CourseStatus.Rejected, "مرفوضة"), (CourseStatus.Approved, "معتمدة"),
        (CourseStatus.Finished, "منتهية"), (CourseStatus.Cancelled, "ملغاة"),
        (CourseStatus.AwaitingAccount, "\u0628\u0627\u0646\u062a\u0638\u0627\u0631 \u0625\u0646\u0634\u0627\u0621 \u062d\u0633\u0627\u0628 \u0627\u0644\u0645\u062f\u0631\u0628")
    ];

    public async Task<Result<StatsLookupsDto>> LookupsAsync(long actorUserId,
        RoleCode contextRole, string? userSearch, CancellationToken ct)
    {
        var access = await AccessAsync(actorUserId, contextRole, ct);
        if (access is null) return Result<StatsLookupsDto>.Failure("FORBIDDEN");
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);

        var faculties = access.Global
            ? await db.Centers.AsNoTracking().Where(x => x.IsFaculty && x.IsActive)
                .OrderBy(x => x.NameAr)
                .Select(x => new StatsOptionDto(x.Id, x.NameAr)).ToListAsync(ct)
            : await db.Centers.AsNoTracking()
                .Where(x => access.CenterIds.Contains(x.Id) && x.IsActive)
                .OrderBy(x => x.NameAr)
                .Select(x => new StatsOptionDto(x.Id, x.NameAr)).ToListAsync(ct);
        if (access.Global)
            faculties.Insert(0, new StatsOptionDto(0, "الإداريون"));
        string term = userSearch?.Trim() ?? string.Empty;
        var users = new List<StatsOptionDto>();
        if (term.Length is >= 2 and <= 100)
        {
            var query = db.Users.AsNoTracking().Where(x => x.IsActive &&
                (x.FullNameAr.Contains(term) || x.LoginName.Contains(term)));
            if (!admin) query = query.Where(x => x.AccountType != AccountType.Employee ||
                x.Employee != null && x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus);
            if (!access.Global)
                query = query.Where(x => x.AccountType == AccountType.Employee &&
                    x.Employee != null && x.Employee.CenterId != null &&
                    access.CenterIds.Contains(x.Employee.CenterId.Value));
            users = await query.OrderBy(x => x.FullNameAr).Take(30)
                .Select(x => new StatsOptionDto(x.Id, x.FullNameAr)).ToListAsync(ct);
        }
        return Result<StatsLookupsDto>.Success(new(access.Global, true,
            faculties, users));
    }

    public async Task<Result<DashboardStatsDto>> DashboardAsync(long actorUserId,
        StatsFilter filter, CancellationToken ct)
    {
        if (filter.CenterId is < 0 || filter.UserId is <= 0 ||
            filter.FromDate > filter.ToDate)
            return Result<DashboardStatsDto>.Failure("QUERY_INVALID");
        var access = await AccessAsync(actorUserId, filter.ContextRole, ct);
        if (access is null) return Result<DashboardStatsDto>.Failure("FORBIDDEN");
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);

        long? centerId = access.Global ? filter.CenterId : null;
        long? userId = filter.UserId;
        if (!admin && userId is { } visibilityUserId &&
            await db.Users.AsNoTracking().AnyAsync(x => x.Id == visibilityUserId &&
                x.AccountType == AccountType.Employee && (x.Employee == null ||
                    x.Employee.EmpStatus != UniversityOrganizationRules.ActiveEmployeeStatus), ct))
            return Result<DashboardStatsDto>.Failure("FORBIDDEN");
        if (!access.Global && userId is { } scopedUser &&
            !await db.Users.AsNoTracking().AnyAsync(x => x.Id == scopedUser && x.IsActive &&
                x.AccountType == AccountType.Employee && x.Employee != null &&
                x.Employee.CenterId != null &&
                access.CenterIds.Contains(x.Employee.CenterId.Value), ct))
            return Result<DashboardStatsDto>.Failure("FORBIDDEN");
        IQueryable<Course> query = db.Courses.IgnoreQueryFilters().AsNoTracking();
        if (!access.Global && userId is null)
            query = query.Where(x => access.CenterIds.Contains(x.ProposingCenterId ?? -1) ||
                access.CenterIds.Contains(x.TargetCenterId ?? -1));
        if (centerId == 0)
            query = query.Where(x => x.AudienceType == AudienceType.Administrative);
        else if (centerId is { } center)
            query = query.Where(x => x.ProposingCenterId == center || x.TargetCenterId == center);
        if (userId is { } person)
            query = query.Where(x => x.InstructorUserId == person ||
                db.CourseParticipants.Any(p => p.CourseId == x.Id && p.UserId == person && p.IsActive));
        if (filter.FromDate is { } from)
            query = query.Where(x => x.PlannedStartDate >= from);
        if (filter.ToDate is { } to)
            query = query.Where(x => x.PlannedStartDate <= to);

        var courses = await query.Select(x => new CourseStatRow(x.Id, x.Status, x.AudienceType,
            x.PlannedStartDate, x.ProposingCenterId, x.ProposingCenter == null
                ? null : x.ProposingCenter.NameAr, x.TargetCenterId,
            x.TargetCenter == null ? null : x.TargetCenter.NameAr,
            x.Track == null ? "غير مصنف" : x.Track.NameAr)).ToListAsync(ct);
        long[] courseIds = courses.Select(x => x.Id).ToArray();
        int participants = courseIds.Length == 0 ? 0 : await db.CourseParticipants
            .AsNoTracking().CountAsync(x => courseIds.Contains(x.CourseId) && x.IsActive &&
                (userId == null || x.UserId == userId), ct);
        int certificates = courseIds.Length == 0 ? 0 : await db.Certificates
            .AsNoTracking().CountAsync(x => courseIds.Contains(x.CourseId) && x.IsActive &&
                x.Status == CertificateStatus.Issued && (userId == null || x.UserId == userId), ct);

        var participantMonths = courseIds.Length == 0
            ? []
            : await db.CourseParticipants.AsNoTracking()
                .Where(x => courseIds.Contains(x.CourseId) && x.IsActive &&
                    (userId == null || x.UserId == userId))
                .Select(x => new { x.CourseId, x.JoinedAt }).ToListAsync(ct);
        var monthKeys = courses.Where(x => x.StartDate.HasValue)
            .Select(x => (x.StartDate!.Value.Year, x.StartDate.Value.Month))
            .Concat(participantMonths.Select(x => (x.JoinedAt.Year, x.JoinedAt.Month)))
            .Distinct().OrderBy(x => x.Year).ThenBy(x => x.Month).TakeLast(12).ToList();
        var monthly = monthKeys.Select(m => new StatsMonthlyDto($"{m.Month:D2}/{m.Year}",
            courses.Count(x => x.StartDate.HasValue && x.StartDate.Value.Year == m.Year &&
                x.StartDate.Value.Month == m.Month),
            participantMonths.Count(x => x.JoinedAt.Year == m.Year && x.JoinedAt.Month == m.Month)))
            .ToList();

        string scopeLabel = "الجامعة كاملة";
        if (!access.Global)
            scopeLabel = access.CenterNames.Count == 0 ? "كلية العميد" : string.Join("، ", access.CenterNames);
        else if (centerId == 0)
            scopeLabel = "الإداريون";
        else if (centerId is { } selectedCenter)
            scopeLabel = await db.Centers.AsNoTracking().Where(x => x.Id == selectedCenter)
                .Select(x => x.NameAr).SingleOrDefaultAsync(ct) ?? "الكلية المحددة";

        StatsPersonDto? personStats = null;
        if (userId is { } selectedUser)
        {
            string? name = await db.Users.AsNoTracking().Where(x => x.Id == selectedUser && x.IsActive)
                .Select(x => x.FullNameAr).SingleOrDefaultAsync(ct);
            if (name is null) return Result<DashboardStatsDto>.Failure("USER_NOT_FOUND");
            personStats = new(selectedUser, name, courses.Count,
                courses.Count(x => x.Status == CourseStatus.Finished), certificates);
        }

        int finished = courses.Count(x => x.Status == CourseStatus.Finished);
        var byStatus = StatusLabels.Select(s => new StatsSeriesDto(s.Label,
            courses.Count(x => x.Status == s.Status))).ToList();
        var byFaculty = courses.GroupBy(x => x.AudienceType == AudienceType.Administrative
                ? "الإداريون" : x.ProposingCenterName ?? x.TargetCenterName ?? "غير محددة")
            .Select(x => new StatsSeriesDto(x.Key, x.Count())).OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label).ToList();
        var byTrack = courses.GroupBy(x => x.TrackName)
            .Select(x => new StatsSeriesDto(x.Key, x.Count())).OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label).ToList();
        return Result<DashboardStatsDto>.Success(new(scopeLabel, courses.Count,
            participants, certificates, finished,
            courses.Count == 0 ? 0 : Math.Round(finished * 100m / courses.Count, 1),
            byStatus, byFaculty, byTrack, monthly, personStats));
    }

    private async Task<StatsAccess?> AccessAsync(long actorUserId, RoleCode contextRole,
        CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool global = contextRole == RoleCode.Admin && effective.HasMode(RoleCode.Admin) &&
                effective.Keys.Contains("page.admin.dashboard") ||
            contextRole == RoleCode.Quality && effective.HasMode(RoleCode.Quality) &&
                effective.Keys.Contains("page.quality.dashboard");
        if (global) return new(true, [], []);
        if (contextRole != RoleCode.Dean || !effective.IsUnitHead ||
            !effective.Keys.Contains("page.dean.stats")) return null;
        var allowedCenters = effective.DeanScopeCenterIds.ToArray();
        var centers = await db.Centers.AsNoTracking().Where(x => allowedCenters.Contains(x.Id))
            .Select(x => new { Id = x.Id, Name = x.NameAr }).ToListAsync(ct);
        return new(false, centers.Select(x => x.Id).ToArray(),
            centers.Select(x => x.Name).ToArray());
    }

    private sealed record StatsAccess(bool Global, IReadOnlyCollection<long> CenterIds,
        IReadOnlyCollection<string> CenterNames);
    private sealed record CourseStatRow(long Id, CourseStatus Status, AudienceType AudienceType,
        DateOnly? StartDate,
        long? ProposingCenterId, string? ProposingCenterName, long? TargetCenterId,
        string? TargetCenterName, string TrackName);
}

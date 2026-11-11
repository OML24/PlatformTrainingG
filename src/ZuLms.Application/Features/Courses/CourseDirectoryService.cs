using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public sealed class CourseDirectoryService(IAppDbContext db,
    IClock clock, IPermissionService permissions, ISettingsService settings) : ICourseDirectoryService
{
    public async Task<Result<CourseDirectoryLookupsDto>> LookupsAsync(
        long actorUserId, CourseDirectoryView view, CancellationToken ct)
    {
        if (await ScopeAsync(actorUserId, view, ct) is null)
            return Result<CourseDirectoryLookupsDto>.Failure("FORBIDDEN");
        var centers = await db.Centers.AsNoTracking()
            .Where(x => x.IsFaculty && x.IsActive).OrderBy(x => x.NameAr)
            .Select(x => new DeanCenterOptionDto(x.Id, x.NameAr)).ToListAsync(ct);
        if (view == CourseDirectoryView.Dean)
        {
            var allowed = (await permissions.GetEffectiveAsync(actorUserId, ct)).DeanScopeCenterIds;
            centers = centers.Where(x => allowed.Contains(x.Id)).ToList();
        }
        else centers.Insert(0, new DeanCenterOptionDto(0, "الإداريون"));
        var tracks = await db.TrainingTracks.AsNoTracking()
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.NameAr)
            .Select(x => new TrackOptionDto(x.Id, x.NameAr)).ToListAsync(ct);
        return Result<CourseDirectoryLookupsDto>.Success(new(centers, tracks));
    }

    public async Task<Result<CourseDirectoryPageDto>> ListAsync(long actorUserId,
        CourseDirectoryFilter filter, CancellationToken ct)
    {
        var scoped = await ScopeAsync(actorUserId, filter.View, ct);
        if (scoped is null) return Result<CourseDirectoryPageDto>.Failure("FORBIDDEN");
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (filter.Page < 1 || pageSize < 1 || filter.Page > int.MaxValue / pageSize ||
            filter.Search?.Length > 100 || filter.CenterId is < 0 ||
            filter.TrackId is <= 0 ||
            filter.FromDate > filter.ToDate ||
            filter.Status is { } status && !Enum.IsDefined(status) ||
            filter.DeliveryMode is { } mode && !Enum.IsDefined(mode))
            return Result<CourseDirectoryPageDto>.Failure("QUERY_INVALID");
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);
        var query = ApplyFilter(scoped, filter, admin);
        int total = await query.CountAsync(ct);
        var rows = await Project(query.OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((filter.Page - 1) * pageSize).Take(pageSize), admin).ToListAsync(ct);
        return Result<CourseDirectoryPageDto>.Success(new(filter.Page,
            pageSize, total, rows));
    }

    public async Task<Result<CourseDirectoryDetailsDto>> GetAsync(long actorUserId,
        long courseId, CourseDirectoryView view, CancellationToken ct)
    {
        var scoped = await ScopeAsync(actorUserId, view, ct);
        if (scoped is null)
            return Result<CourseDirectoryDetailsDto>.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool admin = effective.HasMode(RoleCode.Admin);
        var row = await Project(scoped.Where(x => x.Id == courseId), admin)
            .SingleOrDefaultAsync(ct);
        if (row is null)
            return Result<CourseDirectoryDetailsDto>.Failure("COURSE_NOT_FOUND");
        var details = await db.Courses.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == courseId)
            .Select(x => new
            {
                x.TrackId,
                x.TargetCenterId,
                x.ObjectivesAr,
                x.ObjectivesEn,
                x.TargetAudienceAr,
                x.TargetAudienceEn,
                x.TotalHours,
                x.DeliveryMode,
                x.AudienceType,
                TargetCenterNameAr = x.TargetCenter == null
                    ? null : x.TargetCenter.NameAr,
                x.RegistrationDeadline,
                x.MaxParticipants,
                x.EvaluationEndDate,
                x.AttendanceThresholdPercentOverride,
                x.HasExam,
                x.ManagedByQuality,
                x.CancellationReason,
                x.HistoricalNotes,
                x.InstructorKind,
                x.InstructorUserId,
                x.InstructorNameEn,
                x.InstructorOrgAr,
                x.InstructorOrgEn,
                HiddenEmployee = !admin && x.InstructorUser != null &&
                    x.InstructorUser.AccountType == AccountType.Employee &&
                    (x.InstructorUser.Employee == null ||
                     x.InstructorUser.Employee.EmpStatus != UniversityOrganizationRules.ActiveEmployeeStatus)
            }).SingleAsync(ct);
        bool canViewFiles = effective.HasMode(RoleCode.Admin) &&
            effective.Keys.Contains("page.admin.courses") ||
            effective.HasMode(RoleCode.Quality) &&
            effective.Keys.Contains("page.quality.courses");
        bool canViewHistory = canViewFiles ||
            (view is CourseDirectoryView.Dean or CourseDirectoryView.Auto) &&
            (effective.Roles.Contains(RoleCode.Dean) || effective.Roles.Contains(RoleCode.CenterManager)) &&
            effective.Keys.Contains("page.dean.courses");
        var history = canViewHistory
            ? await db.CourseStatusHistorys.AsNoTracking()
                .Where(x => x.CourseId == courseId)
                .OrderBy(x => x.ChangedAt).ThenBy(x => x.Id)
                .Select(x => new CourseHistoryDto(x.FromStatus, x.ToStatus,
                    x.ChangedAt, x.Reason)).ToListAsync(ct)
            : [];
        var files = canViewFiles
            ? await db.CourseFiles.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.IsActive &&
                (x.File!.Kind == FileKind.ProposalCouncilLetter ||
                 x.File.Kind == FileKind.InstructorCv ||
                 x.File.Kind == FileKind.CancellationLetter))
            .OrderBy(x => x.Id)
            .Select(x => new CourseFileDto(x.FileId, x.Purpose,
                x.File!.OriginalName, x.File.MimeType)).ToListAsync(ct)
            : [];
        return Result<CourseDirectoryDetailsDto>.Success(new(row,
            details.ObjectivesAr, details.ObjectivesEn,
            details.TargetAudienceAr, details.TargetAudienceEn,
            details.TotalHours, details.DeliveryMode,
            details.AudienceType, details.TargetCenterNameAr,
            details.RegistrationDeadline, details.MaxParticipants,
            details.EvaluationEndDate, details.HasExam,
            details.ManagedByQuality, details.CancellationReason,
            details.HistoricalNotes, history, files,
            effective.HasMode(RoleCode.Admin) &&
                effective.Keys.Contains("page.admin.courses"),
            // Course edit actions (exam requirement, data, instructor, threshold, evaluation end) are Admin-only.
            !row.IsHistorical && effective.Keys.Contains("exam.requirement.edit") &&
                effective.HasMode(RoleCode.Admin),
            !row.IsHistorical && effective.Keys.Contains("course.edit") &&
                effective.HasMode(RoleCode.Admin),
            effective.Keys.Contains("course.force_status") &&
                effective.HasMode(RoleCode.Admin),
            details.TrackId, details.TargetCenterId,
            details.AttendanceThresholdPercentOverride,
            effective.Keys.Contains("evaluation.extend_period") &&
                effective.HasMode(RoleCode.Admin),
            details.InstructorKind, details.HiddenEmployee ? null : details.InstructorUserId,
            details.HiddenEmployee ? null : details.InstructorNameEn,
            details.HiddenEmployee ? null : details.InstructorOrgAr,
            details.HiddenEmployee ? null : details.InstructorOrgEn,
            effective.Keys.Contains("course.edit") &&
                effective.HasMode(RoleCode.Admin)));
    }

    private async Task<IQueryable<Course>?> ScopeAsync(long actorUserId,
        CourseDirectoryView view, CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        IQueryable<Course> query = db.Courses.IgnoreQueryFilters().AsNoTracking();
        long[] deanCenters = effective.DeanScopeCenterIds.ToArray();
        bool quality = effective.HasMode(RoleCode.Quality);
        if (view == CourseDirectoryView.Auto)
        {
            view = effective.HasMode(RoleCode.Admin) &&
                    effective.Keys.Contains("page.admin.courses") ||
                   quality && effective.Keys.Contains("page.quality.courses")
                ? CourseDirectoryView.All
                : effective.IsUnitHead &&
                  effective.Keys.Contains("page.dean.courses")
                    ? CourseDirectoryView.Dean
                    : effective.Keys.Contains("page.instructor.courses")
                        ? CourseDirectoryView.Instructor : CourseDirectoryView.Member;
        }
        return view switch
        {
            CourseDirectoryView.All when effective.HasMode(RoleCode.Admin) &&
                effective.Keys.Contains("page.admin.courses") => query,
            CourseDirectoryView.All when quality &&
                effective.Keys.Contains("page.quality.courses") => query.Where(x => x.ManagedByQuality),
            CourseDirectoryView.Dean when
                effective.IsUnitHead &&
                effective.Keys.Contains("page.dean.courses") =>
                query.Where(x => x.ProposingCenterId.HasValue && deanCenters.Contains(x.ProposingCenterId.Value) ||
                    x.TargetCenterId.HasValue && deanCenters.Contains(x.TargetCenterId.Value)),
            CourseDirectoryView.Instructor when quality && effective.Keys.Contains("page.instructor.courses") =>
                query.Where(x => x.ManagedByQuality && (x.Status == CourseStatus.Approved || x.Status == CourseStatus.Finished)),
            CourseDirectoryView.Instructor when (effective.HasMode(RoleCode.Instructor) || effective.HasMode(RoleCode.ExternalInstructor)) &&
                effective.Keys.Contains("page.instructor.courses") =>
                query.Where(x => (x.Status == CourseStatus.Approved || x.Status == CourseStatus.Finished) && x.InstructorUserId == actorUserId),
            CourseDirectoryView.Member when
                effective.Roles.Contains(RoleCode.Member) &&
                effective.Keys.Contains("page.member.enrolled") =>
                query.Where(x => db.CourseParticipants.Any(p => p.CourseId == x.Id &&
                    p.UserId == actorUserId && p.IsActive)),
            CourseDirectoryView.Available when
                effective.Roles.Contains(RoleCode.Member) &&
                effective.Keys.Contains("participants.self_register") =>
                await AvailableScopeAsync(query, actorUserId, ct),
            _ => null
        };
    }

    private async Task<IQueryable<Course>> AvailableScopeAsync(IQueryable<Course> query,
        long actorUserId, CancellationToken ct)
    {
        var employee = await db.Users.AsNoTracking()
            .Where(x => x.Id == actorUserId && x.IsActive &&
                x.AccountType == AccountType.Employee && x.Employee != null &&
                x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus)
            .Select(x => new { x.Employee!.CenterId, x.Employee.IsAcademic })
            .SingleOrDefaultAsync(ct);
        if (employee is null) return query.Where(x => false);
        DateTimeOffset now = clock.UtcNow;
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,
            clock.Amman).DateTime);
        return SelfRegistrationCourseQuery.Apply(query, db, actorUserId,
            employee.CenterId, employee.IsAcademic, now, today);
    }

    private static IQueryable<Course> ApplyFilter(IQueryable<Course> query,
        CourseDirectoryFilter filter, bool admin)
    {
        string term = ArabicSearchNormalizer.Normalize(filter.Search);
        string identifierText = filter.Search?.Trim().TrimStart('#') ?? string.Empty;
        long? identifier = long.TryParse(identifierText, out long parsedId) &&
            parsedId > 0 ? parsedId : null;
        if (term.Length > 0)
            query = query.Where(x => identifier != null && x.Id == identifier.Value ||
                x.TitleAr.Contains(term) ||
                x.TitleEn != null && x.TitleEn.Contains(term) ||
                (admin || x.InstructorUser == null ||
                 x.InstructorUser.AccountType != AccountType.Employee ||
                 x.InstructorUser.Employee != null &&
                 x.InstructorUser.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus) &&
                    x.SearchText.Contains(term) ||
                x.CourseNo.Contains(term));
        if (filter.Status is { } status)
            query = query.Where(x => x.Status == status);
        if (filter.CenterId == 0)
            query = query.Where(x => x.AudienceType == AudienceType.Administrative);
        else if (filter.CenterId is { } center)
            query = query.Where(x => x.ProposingCenterId == center || x.TargetCenterId == center);
        if (filter.TrackId is { } track)
            query = query.Where(x => x.TrackId == track);
        if (filter.DeliveryMode is { } mode)
            query = query.Where(x => x.DeliveryMode == mode);
        if (filter.FromDate is { } from)
            query = query.Where(x => x.PlannedStartDate >= from);
        if (filter.ToDate is { } to)
            query = query.Where(x => x.PlannedStartDate <= to);
        if (filter.IsActive is { } active)
            query = query.Where(x => x.IsActive == active);
        if (filter.IsArchived is { } archived)
            query = query.Where(x => x.IsArchived == archived);
        return query;
    }

    private IQueryable<CourseDirectoryRowDto> Project(IQueryable<Course> query, bool admin) =>
        query.Select(x => new CourseDirectoryRowDto(x.Id, x.CourseNo,
            x.TitleAr, x.TitleEn, x.Status, x.Origin,
            x.AudienceType == AudienceType.Administrative ? "الإداريون" :
                x.ProposingCenter == null ? null : x.ProposingCenter.NameAr,
            x.Track!.NameAr,
            !admin && x.InstructorUser != null &&
                x.InstructorUser.AccountType == AccountType.Employee &&
                (x.InstructorUser.Employee == null ||
                 x.InstructorUser.Employee.EmpStatus != UniversityOrganizationRules.ActiveEmployeeStatus)
                    ? "—" : x.InstructorNameAr,
            x.PlannedStartDate,
            x.PlannedEndDate, x.IsHistorical, x.IsActive, x.IsArchived,
            db.CourseParticipants.Count(p => p.CourseId == x.Id && p.IsActive),
            x.Version));
}

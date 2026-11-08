using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public sealed class CourseProposalQueryService(IAppDbContext db,
    IPermissionService permissions, ISettingsService settings) : ICourseProposalQueryService
{
    public async Task<Result<CourseProposalPageDto>> ListMineAsync(long actorUserId,
        int page, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.dean.courses", ct))
            return Result<CourseProposalPageDto>.Failure("FORBIDDEN");
        return await PageAsync(db.Courses.AsNoTracking().Where(x =>
            x.Origin == CourseOrigin.DeanProposal && x.ProposedByUserId == actorUserId),
            page, ct);
    }

    public async Task<Result<CourseProposalPageDto>> ListPendingAsync(long actorUserId,
        int page, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.quality.proposals", ct))
            return Result<CourseProposalPageDto>.Failure("FORBIDDEN");
        return await PageAsync(db.Courses.AsNoTracking().Where(x =>
            x.Origin == CourseOrigin.DeanProposal && x.Status == CourseStatus.Proposed),
            page, ct);
    }

    private async Task<Result<CourseProposalPageDto>> PageAsync(IQueryable<Course> query,
        int page, CancellationToken ct)
    {
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (page < 1 || pageSize < 1 || page > int.MaxValue / pageSize)
            return Result<CourseProposalPageDto>.Failure("QUERY_INVALID");
        int total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Status == CourseStatus.Returned)
            .ThenByDescending(x => x.StatusChangedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new CourseProposalRowDto(x.Id, x.Version, x.CourseNo,
                x.TitleAr, x.ProposingCenter!.NameAr, x.Status,
                x.CurrentRevisionNo, x.StatusChangedAt, x.LastReturnNotes,
                x.ProposingCenterId!.Value))
            .ToListAsync(ct);
        return Result<CourseProposalPageDto>.Success(new(page, pageSize, total, rows));
    }

    public async Task<Result<CourseProposalDetailsDto>> GetAsync(long actorUserId,
        long courseId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().Include(x => x.ProposingCenter)
            .SingleOrDefaultAsync(x => x.Id == courseId &&
                x.Origin == CourseOrigin.DeanProposal, ct);
        if (course is null) return Result<CourseProposalDetailsDto>.Failure("COURSE_NOT_FOUND");
        bool quality = await permissions.HasAsync(actorUserId, "page.quality.proposals", ct);
        bool dean = course.ProposedByUserId == actorUserId &&
            await permissions.HasAsync(actorUserId, "page.dean.courses", ct);
        if (!quality && !dean) return Result<CourseProposalDetailsDto>.Failure("FORBIDDEN");
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);
        var revisionRows = await db.CourseProposalRevisions.AsNoTracking()
            .Where(x => x.CourseId == courseId).OrderBy(x => x.RevisionNo).ToListAsync(ct);
        var revisions = revisionRows.Select(x => new CourseProposalRevisionDto(
            x.RevisionNo, x.SubmittedAt, x.Decision, x.DecisionNotes,
            JsonSerializer.Deserialize<CourseProposalInput>(x.SnapshotJson) ??
                throw new InvalidDataException("Invalid proposal revision snapshot."))).ToList();
        if (!admin)
        {
            long[] instructorIds = revisions.Where(x => x.Input.InternalInstructorUserId.HasValue)
                .Select(x => x.Input.InternalInstructorUserId!.Value)
                .Append(course.InstructorUserId ?? -1).Where(x => x > 0).Distinct().ToArray();
            long[] hiddenIds = await db.Users.AsNoTracking()
                .Where(x => instructorIds.Contains(x.Id) && x.AccountType == AccountType.Employee &&
                    (x.Employee == null || x.Employee.EmpStatus != UniversityOrganizationRules.ActiveEmployeeStatus))
                .Select(x => x.Id).ToArrayAsync(ct);
            revisions = revisions.Select(x => hiddenIds.Contains(x.Input.InternalInstructorUserId ?? -1)
                ? x with { Input = x.Input with { InternalInstructorUserId = null } } : x).ToList();
            if (hiddenIds.Contains(course.InstructorUserId ?? -1)) course.InstructorUserId = null;
        }
        var row = new CourseProposalRowDto(course.Id, course.Version,
            course.CourseNo, course.TitleAr, course.ProposingCenter!.NameAr,
            course.Status, course.CurrentRevisionNo, course.StatusChangedAt,
            course.LastReturnNotes, course.ProposingCenterId!.Value);
        string? instructorLogin = course.InstructorUserId is long instructorId
            ? await db.Users.Where(x => x.Id == instructorId)
                .Select(x => x.LoginName).SingleOrDefaultAsync(ct) : null;
        int period = await settings.GetAsync<int>("evaluation.default_period_days", ct);
        DateOnly suggested = revisions[^1].Input.PlannedEndDate.AddDays(period);
        return Result<CourseProposalDetailsDto>.Success(new(row, revisions,
            course.EvaluationEndDate, course.AudienceType, course.TargetCenterId,
            course.RegistrationDeadline, course.MaxParticipants, course.HasExam,
            course.InstructorKind, course.InstructorUserId, course.ManagedByQuality,
            instructorLogin, suggested));
    }

    public async Task<Result<DeanProposalLookupsDto>> DeanLookupsAsync(long actorUserId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.dean.propose", ct))
            return Result<DeanProposalLookupsDto>.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        var centers = await db.Centers.AsNoTracking().Where(x => effective.DeanScopeCenterIds.Contains(x.Id) &&
            x.IsActive && x.IsFaculty).Select(x => new DeanCenterOptionDto(x.Id, x.NameAr)).ToListAsync(ct);
        var tracks = await db.TrainingTracks.AsNoTracking()
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.NameAr)
            .Select(x => new TrackOptionDto(x.Id, x.NameAr)).ToListAsync(ct);
        return Result<DeanProposalLookupsDto>.Success(new(centers, tracks));
    }

    public async Task<Result<IReadOnlyList<InstructorOptionDto>>> SearchFacultyMembersAsync(
        long actorUserId, long centerId, string? search, CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool allowed = effective.HasMode(RoleCode.Dean) &&
            (effective.Keys.Contains("page.dean.propose") || effective.Keys.Contains("faculty.members.view"));
        if (!allowed)
            return Result<IReadOnlyList<InstructorOptionDto>>.Failure("FORBIDDEN");
        long[] centers = effective.DeanScopeCenterIds.ToArray();
        long effectiveCenterId = centerId > 0 ? centerId : centers.FirstOrDefault();
        if (effectiveCenterId <= 0 || !centers.Contains(effectiveCenterId))
            return Result<IReadOnlyList<InstructorOptionDto>>.Failure("FORBIDDEN");
        string term = ArabicSearchNormalizer.Normalize(search);
        var query = db.Users.AsNoTracking().Where(x => x.IsActive &&
            x.AccountType == AccountType.Employee && x.Employee != null &&
            x.Employee.CenterId == effectiveCenterId &&
            x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus);
        if (term.Length > 0)
            query = query.Where(x => x.SearchText.Contains(term) ||
                x.LoginName.Contains(term));
        var rows = await query.OrderBy(x => x.FullNameAr).ThenBy(x => x.Id)
            .Select(x => new InstructorOptionDto(x.Id, x.FullNameAr,
                x.LoginName, x.FullNameEn, x.Employee!.Department != null
                    ? x.Employee.Department.NameAr : null)).ToListAsync(ct);
        return Result<IReadOnlyList<InstructorOptionDto>>.Success(rows);
    }

    public async Task<Result<IReadOnlyList<InstructorOptionDto>>> SearchExternalInstructorsAsync(
        long actorUserId, string? search, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.quality.proposals", ct))
            return Result<IReadOnlyList<InstructorOptionDto>>.Failure("FORBIDDEN");
        string term = ArabicSearchNormalizer.Normalize(search);
        var query = db.Users.AsNoTracking().Where(x => x.IsActive &&
            x.AccountType == AccountType.ExternalInstructor);
        if (term.Length > 0)
            query = query.Where(x => x.SearchText.Contains(term) ||
                x.LoginName.Contains(term));
        var rows = await query.OrderBy(x => x.FullNameAr).ThenBy(x => x.Id)
            .Take(30).Select(x => new InstructorOptionDto(x.Id, x.FullNameAr,
                x.LoginName)).ToListAsync(ct);
        return Result<IReadOnlyList<InstructorOptionDto>>.Success(rows);
    }

    public async Task<Result<IReadOnlyList<DeanCenterOptionDto>>> ListFacultiesAsync(
        long actorUserId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.quality.proposals", ct))
            return Result<IReadOnlyList<DeanCenterOptionDto>>.Failure("FORBIDDEN");
        var rows = await db.Centers.AsNoTracking().Where(x => x.IsFaculty && x.IsActive)
            .OrderBy(x => x.NameAr)
            .Select(x => new DeanCenterOptionDto(x.Id, x.NameAr)).ToListAsync(ct);
        return Result<IReadOnlyList<DeanCenterOptionDto>>.Success(rows);
    }
}

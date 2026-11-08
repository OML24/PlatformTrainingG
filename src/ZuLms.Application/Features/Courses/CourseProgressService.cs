using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public sealed class CourseProgressService(IAppDbContext db,
    ISettingsService settings, IEligibilitySnapshotService eligibility,
    IPermissionService permissions)
    : ICourseProgressService
{
    public async Task<Result<CourseProgressDto>> GetMineAsync(long actorId,
        long courseId, CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        if (!effective.HasMode(RoleCode.Member) || !effective.Keys.Contains("page.member.enrolled"))
            return Result<CourseProgressDto>.Failure("FORBIDDEN");
        var participant = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.UserId == actorId &&
                x.IsActive)
            .Select(x => new { x.Id }).SingleOrDefaultAsync(ct);
        if (participant is null)
            return Result<CourseProgressDto>.Failure("FORBIDDEN");
        return await BuildAsync(courseId, participant.Id, ct);
    }

    public async Task<Result<IReadOnlyList<CourseParticipantProgressDto>>>
        GetRosterAsync(long actorId, long courseId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId,
            "page.instructor.participants", ct))
            return Result<IReadOnlyList<CourseParticipantProgressDto>>
                .Failure("FORBIDDEN");
        var course = await db.Courses.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == courseId, ct);
        if (course is null)
            return Result<IReadOnlyList<CourseParticipantProgressDto>>
                .Failure("COURSE_NOT_FOUND");
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        bool admin = effective.HasMode(RoleCode.Admin);
        bool scopedInstructor = (effective.HasMode(RoleCode.Instructor) || effective.HasMode(RoleCode.ExternalInstructor)) &&
            course.InstructorUserId == actorId;
        bool scopedQuality = effective.HasMode(RoleCode.Quality) && course.ManagedByQuality;
        if (!admin && !scopedInstructor && !scopedQuality)
            return Result<IReadOnlyList<CourseParticipantProgressDto>>
                .Failure("FORBIDDEN");
        var participants = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.IsActive &&
                (admin || x.User!.Employee != null &&
                    x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus))
            .OrderBy(x => x.User!.FullNameAr)
            .Select(x => new
            {
                x.Id,
                x.User!.FullNameAr,
                x.User.LoginName
            }).ToListAsync(ct);
        var rows = new List<CourseParticipantProgressDto>(participants.Count);
        foreach (var participant in participants)
        {
            var progress = await BuildAsync(courseId, participant.Id, ct);
            if (!progress.Ok)
                return Result<IReadOnlyList<CourseParticipantProgressDto>>
                    .Failure(progress.Error!.Code);
            var certificate = await db.Certificates.IgnoreQueryFilters()
                .AsNoTracking().Where(x => x.ParticipantId == participant.Id)
                .OrderByDescending(x => x.IssuedAt)
                .Select(x => new { x.ReferenceNo, x.Status })
                .FirstOrDefaultAsync(ct);
            rows.Add(new CourseParticipantProgressDto(participant.Id,
                participant.FullNameAr, participant.LoginName,
                progress.Value!, certificate?.ReferenceNo,
                certificate?.Status));
        }
        return Result<IReadOnlyList<CourseParticipantProgressDto>>.Success(rows);
    }

    private async Task<Result<CourseProgressDto>> BuildAsync(long courseId,
        long participantId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking()
            .Where(x => x.Id == courseId)
            .Select(x => new
            {
                x.Status,
                x.HasExam,
                x.FinalLecturesCount,
                x.AttendanceThresholdPercentOverride
            })
            .SingleOrDefaultAsync(ct);
        if (course is null)
            return Result<CourseProgressDto>.Failure("COURSE_NOT_FOUND");
        int activeLectures = await db.Lectures.CountAsync(x =>
            x.CourseId == courseId && x.IsActive, ct);
        var participant = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.Id == participantId)
            .Select(x => new { x.HistoricalPassed, x.AdminCertificateOverride })
            .SingleAsync(ct);
        bool final = course.Status == CourseStatus.Finished ||
            participant.AdminCertificateOverride;
        int total = course.Status == CourseStatus.Finished
            ? course.FinalLecturesCount ?? activeLectures : activeLectures;
        int attended = await db.AttendanceRecords.CountAsync(x =>
            x.ParticipantId == participantId && x.IsPresent &&
            x.Lecture!.CourseId == courseId && x.Lecture.IsActive, ct);
        int threshold = course.AttendanceThresholdPercentOverride ??
            await settings.GetAsync<int>("attendance.threshold_percent", ct);
        var exam = course.HasExam ? await db.ExamResults.AsNoTracking()
            .Where(x => x.ParticipantId == participantId && x.IsActive &&
                x.Exam!.CourseId == courseId && x.Exam.IsActive)
            .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id)
            .Select(x => (ExamOutcome?)x.Outcome).FirstOrDefaultAsync(ct) : null;
        var state = await eligibility.EvaluateAsync(courseId,
            participantId, false, ct);
        if (!state.Ok)
            return Result<CourseProgressDto>.Failure(state.Error!.Code);
        if (participant.AdminCertificateOverride)
        {
            attended = Math.Max(attended, total);
            if (course.HasExam) exam = ExamOutcome.Passed;
        }
        var impactStatus = await db.ImpactSubmissions.AsNoTracking()
            .Where(x => x.ParticipantId == participantId && x.IsActive)
            .OrderByDescending(x => x.SequenceNo).ThenByDescending(x => x.Id)
            .Select(x => (ImpactStatus?)x.Status).FirstOrDefaultAsync(ct);
        bool impactApproved = participant.AdminCertificateOverride ||
            impactStatus == ImpactStatus.Approved;
        return Result<CourseProgressDto>.Success(new CourseProgressDto(
            attended, total, AttendanceRules.MinLecturesRequired(total, threshold),
            threshold, final, state.Value!.AttendanceOk, course.HasExam,
            exam, state.Value.ImpactOpen,
            !state.Value.UnmetReasons.Contains("EVALUATION_MISSING"),
            impactApproved,
            state.Value.Eligible,
            final ? state.Value.UnmetReasons : [], participant.HistoricalPassed));
    }
}

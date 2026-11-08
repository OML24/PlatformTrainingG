using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public sealed class ExamService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IEligibilityRecalculationService recalculation,
    IAuditLogger audit) : IExamService
{
    public async Task<Result<ExamDto?>> GetAsync(long actorId, long courseId,
        CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == courseId, ct);
        if (course is null) return Result<ExamDto?>.Failure("COURSE_NOT_FOUND");
        if (course.IsHistorical) return Result<ExamDto?>.Failure("EXAM_NOT_ALLOWED");
        if (!await CanManageAsync(actorId, course, ct))
            return Result<ExamDto?>.Failure("FORBIDDEN");
        if (!course.HasExam) return Result<ExamDto?>.Failure("EXAM_NOT_ALLOWED");
        var exam = await db.CourseExams.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CourseId == courseId && x.IsActive, ct);
        if (exam is null) return Result<ExamDto?>.Success(null);
        bool admin = (await permissions.GetEffectiveAsync(actorId, ct)).HasMode(RoleCode.Admin);
        var participants = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.IsActive &&
                (admin || x.User!.Employee != null &&
                    x.User.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus))
            .OrderBy(x => x.User!.FullNameAr)
            .Select(x => new ExamParticipantDto(x.Id, x.User!.FullNameAr,
                x.User.LoginName, db.ExamResults.Where(r => r.ExamId == exam.Id &&
                    r.ParticipantId == x.Id && r.IsActive)
                    .Select(r => (ExamOutcome?)r.Outcome).FirstOrDefault()))
            .ToListAsync(ct);
        return Result<ExamDto?>.Success(new ExamDto(exam.Id, exam.NameAr,
            participants));
    }

    public async Task<Result<long>> CreateAsync(long actorId, long courseId,
        string nameAr, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "exam.manage", ct))
            return Result<long>.Failure("FORBIDDEN");
        nameAr = nameAr?.Trim() ?? "";
        if (nameAr.Length is < 1 or > 300)
            return Result<long>.Failure("EXAM_NAME_INVALID");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.SingleOrDefaultAsync(x =>
                x.Id == courseId, ct);
            if (course is null) return Result<long>.Failure("COURSE_NOT_FOUND");
            if (course.IsHistorical) return Result<long>.Failure("HISTORICAL_READ_ONLY");
            if (!await CanManageAsync(actorId, course, ct))
                return Result<long>.Failure("FORBIDDEN");
            if (!course.HasExam || course.Status is not
                (CourseStatus.Approved or CourseStatus.Finished))
                return Result<long>.Failure("EXAM_NOT_ALLOWED");
            var exam = await db.CourseExams.SingleOrDefaultAsync(x =>
                x.CourseId == courseId, ct);
            if (exam is null)
            {
                exam = new CourseExam
                {
                    CourseId = courseId,
                    NameAr = nameAr,
                    IsActive = true,
                    CreatedAt = clock.UtcNow,
                    CreatedByUserId = actorId
                };
                db.CourseExams.Add(exam);
            }
            else
            {
                exam.IsActive = true; exam.NameAr = nameAr;
                exam.UpdatedAt = clock.UtcNow; exam.UpdatedByUserId = actorId;
            }
            await audit.LogAsync(new AuditEntry(AuditCategory.Course,
                "exam.create", "إنشاء أو إعادة تفعيل امتحان",
                "Exam created or reactivated", "Course", courseId,
                course.TitleAr, ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result<long>.Success(exam.Id);
        }, ct);
    }

    public async Task<Result> RecordAsync(long actorId, long courseId,
        long participantId, ExamOutcome outcome, CancellationToken ct)
    {
        if (!Enum.IsDefined(outcome)) return Result.Failure("EXAM_OUTCOME_INVALID");
        if (!await permissions.HasAsync(actorId, "exam.manage", ct))
            return Result.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.SingleOrDefaultAsync(x =>
                x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (course.IsHistorical) return Result.Failure("HISTORICAL_READ_ONLY");
            if (!await CanManageAsync(actorId, course, ct))
                return Result.Failure("FORBIDDEN");
            if (!course.HasExam || course.Status is not
                (CourseStatus.Approved or CourseStatus.Finished))
                return Result.Failure("EXAM_NOT_ALLOWED");
            var exam = await db.CourseExams.SingleOrDefaultAsync(x =>
                x.CourseId == courseId && x.IsActive, ct);
            if (exam is null) return Result.Failure("EXAM_NOT_FOUND");
            var participant = await db.CourseParticipants.SingleOrDefaultAsync(x =>
                x.Id == participantId && x.CourseId == courseId && x.IsActive, ct);
            if (participant is null) return Result.Failure("PARTICIPANT_NOT_FOUND");
            var result = await db.ExamResults.SingleOrDefaultAsync(x =>
                x.ExamId == exam.Id && x.ParticipantId == participantId &&
                x.IsActive, ct);
            var previous = result?.Outcome;
            if (previous == outcome) return Result.Success();
            var now = clock.UtcNow;
            if (result is null)
            {
                result = new ExamResult
                {
                    ExamId = exam.Id,
                    ParticipantId = participantId,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedByUserId = actorId
                };
                db.ExamResults.Add(result);
            }
            result.Outcome = outcome; result.RecordedAt = now;
            result.RecordedBy = actorId; result.ChangedAt = previous is null
                ? null : now;
            await db.SaveChangesAsync(ct);
            db.ExamResultHistorys.Add(new ExamResultHistory
            {
                ExamResultId = result.Id,
                OldOutcome = previous,
                NewOutcome = outcome,
                ChangedBy = actorId,
                ChangedAt = now
            });
            var state = await recalculation.RecalculateParticipantAsync(
                courseId, participantId,
                CertificateRevokeReason.ExamResultChanged, actorId, ct);
            if (!state.Ok) return Result.Failure(state.Error!.Code);
            await audit.LogAsync(new AuditEntry(AuditCategory.Course,
                "exam.result.record", "رصد نتيجة امتحان",
                "Exam result recorded", "CourseExam", exam.Id,
                exam.NameAr, AfterJson: JsonSerializer.Serialize(new
                { participantId, previous, outcome }), ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private async Task<bool> CanManageAsync(long actorId, Course course,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "exam.manage", ct))
            return false;
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        return effective.HasMode(RoleCode.Admin) ||
            (effective.HasMode(RoleCode.Instructor) || effective.HasMode(RoleCode.ExternalInstructor)) && course.InstructorUserId == actorId ||
            course.ManagedByQuality && effective.HasMode(RoleCode.Quality);
    }
}

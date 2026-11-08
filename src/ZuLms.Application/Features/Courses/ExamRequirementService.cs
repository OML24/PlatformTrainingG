using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public sealed class ExamRequirementService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IEligibilityRecalculationService recalculation,
    IAuditLogger audit) : IExamRequirementService
{
    public async Task<Result> ChangeAsync(long actorId, long courseId,
        bool hasExam, string? disableReason, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "exam.requirement.edit", ct))
            return Result.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorId, ct);
        bool admin = effective.HasMode(RoleCode.Admin);
        if (!admin && !effective.HasMode(RoleCode.Quality))
            return Result.Failure("FORBIDDEN");
        if (disableReason?.Trim().Length > 1000)
            return Result.Failure("REASON_TOO_LONG");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.SingleOrDefaultAsync(x =>
                x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (!ScopeGuard.AllowsQualityManagedCourse(effective, course.ManagedByQuality))
                return Result.Failure("FORBIDDEN");
            if (course.IsHistorical) return Result.Failure("HISTORICAL_READ_ONLY");
            if (course.Status is not (CourseStatus.Approved or CourseStatus.Finished))
                return Result.Failure("EXAM_NOT_ALLOWED");
            if (course.HasExam == hasExam) return Result.Success();
            var exam = await db.CourseExams.SingleOrDefaultAsync(x =>
                x.CourseId == courseId, ct);
            var now = clock.UtcNow;
            int disabledResults = 0;
            if (!hasExam)
            {
                var results = exam is null ? [] : await db.ExamResults
                    .Where(x => x.ExamId == exam.Id && x.IsActive).ToListAsync(ct);
                if (results.Count > 0)
                {
                    if (!admin) return Result.Failure("EXAM_RESULTS_EXIST");
                    if (!await permissions.HasAsync(actorId,
                        "exam.results.disable", ct))
                        return Result.Failure("FORBIDDEN");
                    if (string.IsNullOrWhiteSpace(disableReason))
                        return Result.Failure("DISABLE_REASON_REQUIRED");
                    disabledResults = results.Count;
                    foreach (var result in results)
                    {
                        result.IsActive = false;
                        result.DeactivatedAt = now;
                        result.DeactivatedBy = actorId;
                        result.DeactivationReason = disableReason.Trim();
                    }
                }
                if (exam is not null) exam.IsActive = false;
            }
            course.HasExam = hasExam;
            course.UpdatedAt = now; course.UpdatedByUserId = actorId;
            await db.SaveChangesAsync(ct);
            var recalc = await recalculation.RecalculateCourseAsync(courseId,
                CertificateRevokeReason.ExamRequirementChanged, actorId, ct);
            if (!recalc.Ok) return recalc;
            await audit.LogAsync(new AuditEntry(AuditCategory.Course,
                "exam.requirement.edit", "تغيير شرط الامتحان",
                "Exam requirement changed", "Course", courseId,
                course.TitleAr, AfterJson: JsonSerializer.Serialize(new
                { hasExam, disabledResults, disableReason }),
                ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }
}

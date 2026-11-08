using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public sealed class LectureDeactivationService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IEligibilityRecalculationService recalculation,
    IAuditLogger audit) : ILectureDeactivationService
{
    public async Task<Result> DeactivateAsync(long actorId, long lectureId,
        string? reason, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "lecture.manage", ct))
            return Result.Failure("FORBIDDEN");
        if (reason?.Trim().Length > 1000)
            return Result.Failure("REASON_TOO_LONG");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var lecture = await db.Lectures.Include(x => x.Course)
                .SingleOrDefaultAsync(x => x.Id == lectureId && x.IsActive, ct);
            if (lecture?.Course is null) return Result.Failure("LECTURE_NOT_FOUND");
            var course = lecture.Course;
            var effective = await permissions.GetEffectiveAsync(actorId, ct);
            bool admin = effective.HasMode(RoleCode.Admin);
            bool instructor = (effective.HasMode(RoleCode.Instructor) || effective.HasMode(RoleCode.ExternalInstructor)) &&
                course.InstructorUserId == actorId ||
                course.ManagedByQuality && effective.HasMode(RoleCode.Quality);
            if (!admin && !instructor) return Result.Failure("FORBIDDEN");
            if (course.Status is not (CourseStatus.Approved or CourseStatus.Finished))
                return Result.Failure("LECTURE_DEACTIVATE_CLOSED");
            bool hasAttendance = await db.AttendanceRecords.AnyAsync(x =>
                x.LectureId == lectureId, ct);
            if (!admin && course.Status == CourseStatus.Finished)
                return Result.Failure("COURSE_FINISHED");
            if (!admin && hasAttendance)
                return Result.Failure("LECTURE_HAS_ATTENDANCE");
            DateTimeOffset now = clock.UtcNow;
            lecture.IsActive = false;
            lecture.DeactivatedAt = now;
            lecture.DeactivatedByUserId = actorId;
            lecture.DeactivationReason = string.IsNullOrWhiteSpace(reason)
                ? null : reason.Trim();
            lecture.UpdatedAt = now; lecture.UpdatedByUserId = actorId;
            await db.SaveChangesAsync(ct);
            if (course.Status == CourseStatus.Finished)
            {
                course.FinalLecturesCount = await db.Lectures.CountAsync(x =>
                    x.CourseId == course.Id && x.IsActive, ct);
                course.UpdatedAt = now; course.UpdatedByUserId = actorId;
                await db.SaveChangesAsync(ct);
            }
            if (course.Status == CourseStatus.Finished)
            {
                var checkedCourse = await recalculation.RecalculateCourseAsync(
                    course.Id, CertificateRevokeReason.AttendanceCorrection,
                    actorId, ct);
                if (!checkedCourse.Ok) return checkedCourse;
            }
            await audit.LogAsync(new AuditEntry(AuditCategory.Lecture,
                "lecture.deactivate", "تعطيل محاضرة",
                "Lecture deactivated", "Lecture", lectureId,
                lecture.TitleAr ?? course.TitleAr,
                CourseId: course.Id, ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }
}

using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public sealed class CourseFinishService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings,
    IEligibilityRecalculationService recalculation, IAuditLogger audit)
    : ICourseFinishService
{
    public async Task<Result<int>> FinishAsync(long actorId, long courseId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorId, "course.finish", ct))
            return Result<int>.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.SingleOrDefaultAsync(x =>
                x.Id == courseId, ct);
            if (course is null) return Result<int>.Failure("COURSE_NOT_FOUND");
            var effective = await permissions.GetEffectiveAsync(actorId, ct);
            bool admin = effective.HasMode(RoleCode.Admin);
            bool scopedInstructor = (effective.HasMode(RoleCode.Instructor) || effective.HasMode(RoleCode.ExternalInstructor)) &&
                course.InstructorUserId == actorId;
            bool scopedQuality = effective.HasMode(RoleCode.Quality) && course.ManagedByQuality;
            if (!admin && !scopedInstructor && !scopedQuality)
                return Result<int>.Failure("FORBIDDEN");
            if (course.Status != CourseStatus.Approved)
                return Result<int>.Failure("FINISH_NOT_ALLOWED");
            int lecturesCount = await db.Lectures.CountAsync(x =>
                x.CourseId == courseId && x.IsActive, ct);
            int minimum = await settings.GetAsync<int>(
                "attendance.min_lectures_to_finish", ct);
            if (lecturesCount < Math.Max(1, minimum))
                return Result<int>.Failure("FINISH_NO_LECTURES");
            DateTimeOffset now = clock.UtcNow;
            course.Status = CourseStatus.Finished;
            course.StatusChangedAt = now;
            course.FinishedAt = now; course.FinishedBy = actorId;
            course.FinalLecturesCount = lecturesCount;
            course.UpdatedAt = now; course.UpdatedByUserId = actorId;
            db.CourseStatusHistorys.Add(new CourseStatusHistory
            {
                CourseId = courseId,
                FromStatus = CourseStatus.Approved,
                ToStatus = CourseStatus.Finished,
                ChangedBy = actorId,
                ChangedAt = now
            });
            var participants = await db.CourseParticipants.AsNoTracking()
                .Where(x => x.CourseId == courseId && x.IsActive)
                .Select(x => new { x.Id, x.UserId }).ToListAsync(ct);
            await db.SaveChangesAsync(ct);
            var recalculated = await recalculation.RecalculateCourseAsync(
                courseId, null, actorId, ct);
            if (!recalculated.Ok)
                return Result<int>.Failure(recalculated.Error!.Code);
            foreach (var participant in participants)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = participant.UserId,
                    Type = NotificationType.CourseFinished,
                    TitleAr = "انتهت الدورة",
                    TitleEn = "Course finished",
                    BodyAr = course.TitleAr,
                    BodyEn = course.TitleEn,
                    LinkUrl = $"/courses/{courseId}",
                    RelatedEntityType = "Course",
                    RelatedEntityId = courseId,
                    CreatedAt = now,
                    IsActive = true
                });
            }
            await audit.LogAsync(new AuditEntry(AuditCategory.Course,
                "course.finish", "إنهاء دورة", "Course finished",
                "Course", courseId, course.TitleAr,
                ActorUserId: actorId), ct);
            await db.SaveChangesAsync(ct);
            return Result<int>.Success(lecturesCount);
        }, ct);
    }
}

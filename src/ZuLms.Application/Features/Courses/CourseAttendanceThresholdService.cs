using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public interface ICourseAttendanceThresholdService
{
    Task<Result> ChangeAsync(long actorUserId, long courseId, uint expectedVersion,
        short? percent, CancellationToken ct);
}

public sealed class CourseAttendanceThresholdService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IEligibilityRecalculationService recalculation,
    IAuditLogger audit) : ICourseAttendanceThresholdService
{
    public async Task<Result> ChangeAsync(long actorUserId, long courseId,
        uint expectedVersion, short? percent, CancellationToken ct)
    {
        var access = await permissions.GetEffectiveAsync(actorUserId, ct);
        if (!access.Keys.Contains("course.edit") ||
            !access.HasMode(RoleCode.Admin) && !access.HasMode(RoleCode.Quality))
            return Result.Failure("FORBIDDEN");
        if (percent is <= 0 or > 100) return Result.Failure("THRESHOLD_INVALID");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (!ScopeGuard.AllowsQualityManagedCourse(access, course.ManagedByQuality)) return Result.Failure("FORBIDDEN");
            if (course.Version != expectedVersion) return Result.Failure("CONFLICT");
            if (course.AttendanceThresholdPercentOverride == percent)
                return Result.Success();
            short? previous = course.AttendanceThresholdPercentOverride;
            course.AttendanceThresholdPercentOverride = percent;
            course.UpdatedAt = clock.UtcNow;
            course.UpdatedByUserId = actorUserId;
            await db.SaveChangesAsync(ct);
            if (course.Status == CourseStatus.Finished)
            {
                var result = await recalculation.RecalculateCourseAsync(courseId,
                    CertificateRevokeReason.AttendanceCorrection, actorUserId, ct);
                if (!result.Ok)
                    throw new InvalidOperationException($"Threshold recalculation failed: {result.Error!.Code}");
            }
            await audit.LogAsync(new AuditEntry(AuditCategory.Course,
                "course.attendance_threshold.edit", "تعديل نسبة حضور الدورة",
                "Course attendance threshold changed", "Course", courseId,
                course.TitleAr, courseId,
                BeforeJson: JsonSerializer.Serialize(new { percent = previous }),
                AfterJson: JsonSerializer.Serialize(new { percent }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }
}

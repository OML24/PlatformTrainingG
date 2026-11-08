using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public sealed record CourseCommonEditInput(uint ExpectedVersion, string TitleAr,
    string? TitleEn, string ObjectivesAr, string? ObjectivesEn,
    string TargetAudienceAr, string? TargetAudienceEn, decimal TotalHours,
    DateOnly PlannedStartDate, DateOnly PlannedEndDate, int? MaxParticipants,
    long TrackId, DeliveryMode DeliveryMode, AudienceType AudienceType,
    long? TargetCenterId, DateTimeOffset? RegistrationDeadline);

public interface ICourseCommonEditService
{
    Task<Result> EditAsync(long actorUserId, long courseId, CourseCommonEditInput input,
        CancellationToken ct);
}

public sealed class CourseCommonEditService(IAppDbContext db, IClock clock,
    IPermissionService permissions, IAuditLogger audit) : ICourseCommonEditService
{
    public async Task<Result> EditAsync(long actorUserId, long courseId,
        CourseCommonEditInput input, CancellationToken ct)
    {
        var access = await permissions.GetEffectiveAsync(actorUserId, ct);
        if (!access.Keys.Contains("course.edit") ||
            !access.HasMode(RoleCode.Admin) && !access.HasMode(RoleCode.Quality))
            return Result.Failure("FORBIDDEN");
        if (string.IsNullOrWhiteSpace(input.TitleAr) || input.TitleAr.Trim().Length > 300 ||
            input.TitleEn?.Trim().Length > 300 ||
            string.IsNullOrWhiteSpace(input.ObjectivesAr) ||
            string.IsNullOrWhiteSpace(input.TargetAudienceAr) ||
            input.TotalHours <= 0 || input.TotalHours > 9999.99m ||
            decimal.Round(input.TotalHours, 2) != input.TotalHours ||
            input.PlannedStartDate == default ||
            input.PlannedEndDate < input.PlannedStartDate ||
            input.MaxParticipants is <= 0 || input.TrackId <= 0 ||
            !Enum.IsDefined(input.DeliveryMode) ||
            !Enum.IsDefined(input.AudienceType) ||
            input.AudienceType == AudienceType.Faculty && input.TargetCenterId is null ||
            input.AudienceType != AudienceType.Faculty && input.TargetCenterId is not null)
            return Result.Failure("COURSE_INVALID");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (!ScopeGuard.AllowsQualityManagedCourse(access, course.ManagedByQuality)) return Result.Failure("FORBIDDEN");
            if (course.Version != input.ExpectedVersion) return Result.Failure("CONFLICT");
            if (!access.HasMode(RoleCode.Admin) &&
                (input.TrackId != course.TrackId ||
                 input.DeliveryMode != course.DeliveryMode))
                return Result.Failure("FORBIDDEN");
            if (input.TrackId != course.TrackId &&
                !await db.TrainingTracks.AnyAsync(x => x.Id == input.TrackId &&
                    x.IsActive && !x.IsArchived, ct))
                return Result.Failure("TRACK_NOT_FOUND");
            if (input.TargetCenterId is long centerId &&
                !await db.Centers.AnyAsync(x => x.Id == centerId &&
                    x.IsFaculty && x.IsActive, ct))
                return Result.Failure("TARGET_FACULTY_INVALID");
            var before = new
            {
                course.TitleAr,
                course.TitleEn,
                course.ObjectivesAr,
                course.ObjectivesEn,
                course.TargetAudienceAr,
                course.TargetAudienceEn,
                course.TotalHours,
                course.PlannedStartDate,
                course.PlannedEndDate,
                course.MaxParticipants,
                course.TrackId,
                course.DeliveryMode,
                course.AudienceType,
                course.TargetCenterId,
                course.RegistrationDeadline
            };
            course.TitleAr = input.TitleAr.Trim();
            course.TitleEn = input.TitleEn?.Trim();
            course.ObjectivesAr = input.ObjectivesAr.Trim();
            course.ObjectivesEn = input.ObjectivesEn?.Trim();
            course.TargetAudienceAr = input.TargetAudienceAr.Trim();
            course.TargetAudienceEn = input.TargetAudienceEn?.Trim();
            course.TotalHours = input.TotalHours;
            course.PlannedStartDate = input.PlannedStartDate;
            course.PlannedEndDate = input.PlannedEndDate;
            course.MaxParticipants = input.MaxParticipants;
            course.TrackId = input.TrackId;
            course.DeliveryMode = input.DeliveryMode;
            course.AudienceType = input.AudienceType;
            course.TargetCenterId = input.TargetCenterId;
            course.RegistrationDeadline = input.RegistrationDeadline;
            course.SearchText = ArabicSearchNormalizer.Normalize(
                $"{course.CourseNo} {course.TitleAr} {course.TitleEn} {course.InstructorNameAr}");
            course.UpdatedAt = clock.UtcNow;
            course.UpdatedByUserId = actorUserId;
            await audit.LogAsync(new AuditEntry(AuditCategory.Course,
                "course.edit", "تعديل بيانات الدورة", "Course details edited",
                "Course", course.Id, course.TitleAr, course.Id,
                JsonSerializer.Serialize(before), JsonSerializer.Serialize(new
                {
                    course.TitleAr,
                    course.TitleEn,
                    course.ObjectivesAr,
                    course.ObjectivesEn,
                    course.TargetAudienceAr,
                    course.TargetAudienceEn,
                    course.TotalHours,
                    course.PlannedStartDate,
                    course.PlannedEndDate,
                    course.MaxParticipants,
                    course.TrackId,
                    course.DeliveryMode,
                    course.AudienceType,
                    course.TargetCenterId,
                    course.RegistrationDeadline
                }), ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }
}

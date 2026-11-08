using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public sealed class SelfRegistrationService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings, IAuditLogger audit) : ISelfRegistrationService
{
    public async Task<Result<AvailableCoursePageDto>> ListAvailableAsync(
        long userId, int page, CancellationToken ct)
    {
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (page < 1 || pageSize < 1 || page > int.MaxValue / pageSize)
            return Result<AvailableCoursePageDto>.Failure("QUERY_INVALID");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || !user.IsActive || user.AccountType != AccountType.Employee ||
            user.EmployeeId is null)
            return Result<AvailableCoursePageDto>.Success(new(page, pageSize, 0, []));
        var employee = await db.Employees.Where(x => x.Id == user.EmployeeId)
            .Select(x => new { x.CenterId, x.IsAcademic }).SingleAsync(ct);
        var query = db.Courses.AsNoTracking().Where(x => x.Status == CourseStatus.Approved &&
            x.IsActive && !x.IsArchived &&
            (x.AudienceType == AudienceType.General ||
                x.AudienceType == AudienceType.Faculty && x.TargetCenterId == employee.CenterId ||
                x.AudienceType == AudienceType.Administrative && !employee.IsAcademic));
        int total = await query.CountAsync(ct);
        var courses = await query.OrderBy(x => x.PlannedStartDate).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AvailableCourseDto(x.Id, x.CourseNo, x.TitleAr, x.TitleEn,
                x.PlannedStartDate, x.PlannedEndDate, x.MaxParticipants,
                x.RegistrationDeadline, db.CourseParticipants.Any(p => p.CourseId == x.Id &&
                    p.UserId == userId && p.IsActive)))
            .ToListAsync(ct);
        return Result<AvailableCoursePageDto>.Success(new(page, pageSize, total, courses));
    }

    public async Task<Result> RegisterAsync(long userId, long courseId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(userId, "participants.self_register", ct))
            return Result.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null || course.Status != CourseStatus.Approved || !course.IsActive ||
                course.IsArchived) return Result.Failure("REG_NOT_ELIGIBLE");
            var user = await db.Users.Include(x => x.Employee)
                .SingleOrDefaultAsync(x => x.Id == userId, ct);
            if (user is null || !user.IsActive || user.AccountType != AccountType.Employee ||
                user.Employee is null || course.AudienceType == AudienceType.Named ||
                course.AudienceType == AudienceType.Faculty &&
                    user.Employee.CenterId != course.TargetCenterId ||
                course.AudienceType == AudienceType.Administrative &&
                    user.Employee.IsAcademic)
                return Result.Failure("REG_NOT_ELIGIBLE");
            if (course.InstructorUserId == userId) return Result.Failure("REG_IS_INSTRUCTOR");
            if (course.RegistrationDeadline is DateTimeOffset deadline && clock.UtcNow > deadline)
                return Result.Failure("REG_CLOSED");
            int active = await db.CourseParticipants.CountAsync(x => x.CourseId == courseId && x.IsActive, ct);
            if (course.MaxParticipants is int max && active >= max)
                return Result.Failure("REG_FULL");
            var participant = await db.CourseParticipants.SingleOrDefaultAsync(x =>
                x.CourseId == courseId && x.UserId == userId, ct);
            if (participant?.IsActive == true) return Result.Failure("REG_ALREADY");
            if (participant is null)
            {
                participant = new CourseParticipant
                {
                    CourseId = courseId, UserId = userId,
                    Source = ParticipantSource.SelfRegistered,
                    JoinedAt = clock.UtcNow, IsActive = true,
                    CreatedAt = clock.UtcNow, CreatedByUserId = userId
                };
                db.CourseParticipants.Add(participant);
            }
            else
            {
                participant.Source = ParticipantSource.SelfRegistered;
                participant.JoinedAt = clock.UtcNow;
                participant.IsActive = true;
                participant.RemovedAt = null;
                participant.RemovedBy = null;
                participant.RemovalReason = null;
                participant.UpdatedAt = clock.UtcNow;
                participant.UpdatedByUserId = userId;
            }
            await audit.LogAsync(new AuditEntry(AuditCategory.Course, "participant.self_register",
                "تسجيل ذاتي في دورة", "Self-registration in course", "Course",
                courseId, course.TitleAr, ActorUserId: userId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }
}

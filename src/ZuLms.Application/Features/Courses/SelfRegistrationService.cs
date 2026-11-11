using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

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
            .Select(x => new { x.CenterId, x.IsAcademic, x.EmpStatus }).SingleAsync(ct);
        if (employee.EmpStatus != UniversityOrganizationRules.ActiveEmployeeStatus)
            return Result<AvailableCoursePageDto>.Success(new(page, pageSize, 0, []));
        DateTimeOffset now = clock.UtcNow;
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,
            clock.Amman).DateTime);
        var query = SelfRegistrationCourseQuery.Apply(db.Courses.AsNoTracking(), db,
            userId, employee.CenterId, employee.IsAcademic, now, today);
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
            var user = await db.Users.Include(x => x.Employee)
                .SingleOrDefaultAsync(x => x.Id == userId, ct);
            if (course is null || user is null || !user.IsActive ||
                user.AccountType != AccountType.Employee || user.Employee is null)
                return Result.Failure("REG_NOT_ELIGIBLE");
            int active = await db.CourseParticipants.CountAsync(x => x.CourseId == courseId && x.IsActive, ct);
            DateTimeOffset now = clock.UtcNow;
            DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,
                clock.Amman).DateTime);
            string? eligibilityError = SelfRegistrationRules.Evaluate(course, userId,
                user.Employee.CenterId, user.Employee.IsAcademic, user.Employee.EmpStatus,
                now, today, active) switch
            {
                SelfRegistrationEligibility.Eligible => null,
                SelfRegistrationEligibility.Instructor => "REG_IS_INSTRUCTOR",
                SelfRegistrationEligibility.Closed => "REG_CLOSED",
                SelfRegistrationEligibility.Full => "REG_FULL",
                _ => "REG_NOT_ELIGIBLE"
            };
            if (eligibilityError is not null) return Result.Failure(eligibilityError);
            var participant = await db.CourseParticipants.SingleOrDefaultAsync(x =>
                x.CourseId == courseId && x.UserId == userId, ct);
            if (participant?.IsActive == true) return Result.Failure("REG_ALREADY");
            if (participant is null)
            {
                participant = new CourseParticipant
                {
                    CourseId = courseId, UserId = userId,
                    Source = ParticipantSource.SelfRegistered,
                    JoinedAt = now, IsActive = true,
                    CreatedAt = now, CreatedByUserId = userId
                };
                db.CourseParticipants.Add(participant);
            }
            else
            {
                participant.Source = ParticipantSource.SelfRegistered;
                participant.JoinedAt = now;
                participant.IsActive = true;
                participant.RemovedAt = null;
                participant.RemovedBy = null;
                participant.RemovalReason = null;
                participant.UpdatedAt = now;
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

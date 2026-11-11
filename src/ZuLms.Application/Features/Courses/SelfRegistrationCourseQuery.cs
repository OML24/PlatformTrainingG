using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

internal static class SelfRegistrationCourseQuery
{
    public static IQueryable<Course> Apply(IQueryable<Course> courses,
        IAppDbContext db, long userId, long? employeeCenterId, bool isAcademic,
        DateTimeOffset now, DateOnly today) => courses.Where(x =>
            x.Status == CourseStatus.Approved && x.IsActive && !x.IsArchived &&
            !x.IsHistorical && x.PlannedEndDate != null &&
            x.PlannedEndDate >= today && x.InstructorUserId != userId &&
            (x.RegistrationDeadline == null || x.RegistrationDeadline >= now) &&
            (x.AudienceType == AudienceType.General ||
             x.AudienceType == AudienceType.Faculty &&
                x.TargetCenterId == employeeCenterId ||
             x.AudienceType == AudienceType.Administrative && !isAcademic) &&
            (x.MaxParticipants == null || db.CourseParticipants.Count(p =>
                p.CourseId == x.Id && p.IsActive) < x.MaxParticipants));
}

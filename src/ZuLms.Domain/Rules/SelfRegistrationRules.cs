using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Rules;

public enum SelfRegistrationEligibility
{
    Eligible,
    NotEligible,
    Instructor,
    Closed,
    Full
}

public static class SelfRegistrationRules
{
    public static SelfRegistrationEligibility Evaluate(Course course, long userId,
        long? employeeCenterId, bool isAcademic, int? employeeStatus,
        DateTimeOffset now, DateOnly today, int activeParticipants)
    {
        if (employeeStatus != UniversityOrganizationRules.ActiveEmployeeStatus ||
            course.Status != CourseStatus.Approved || !course.IsActive ||
            course.IsArchived || course.IsHistorical ||
            course.PlannedEndDate is not { } endDate || endDate < today ||
            !AudienceAllows(course, employeeCenterId, isAcademic))
            return SelfRegistrationEligibility.NotEligible;
        if (course.InstructorUserId == userId)
            return SelfRegistrationEligibility.Instructor;
        if (course.RegistrationDeadline is { } deadline && now > deadline)
            return SelfRegistrationEligibility.Closed;
        if (course.MaxParticipants is { } max && activeParticipants >= max)
            return SelfRegistrationEligibility.Full;
        return SelfRegistrationEligibility.Eligible;
    }

    private static bool AudienceAllows(Course course, long? employeeCenterId,
        bool isAcademic) => course.AudienceType switch
    {
        AudienceType.General => true,
        AudienceType.Faculty => course.TargetCenterId == employeeCenterId,
        AudienceType.Administrative => !isAcademic,
        _ => false
    };
}

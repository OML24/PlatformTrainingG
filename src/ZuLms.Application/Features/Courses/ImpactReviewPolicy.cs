using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public static class ImpactReviewPolicy
{
    public static bool IsDeanDestination(CourseOrigin origin,
        AudienceType audienceType, long? proposingCenterId,
        long? targetCenterId, bool managedByQuality) =>
        origin == CourseOrigin.DeanProposal &&
        audienceType == AudienceType.Faculty &&
        proposingCenterId is not null &&
        targetCenterId == proposingCenterId &&
        !managedByQuality;

    public static bool CanReview(bool isAdmin, bool isQuality, bool isDean,
        IReadOnlyCollection<long> deanCenterIds, CourseOrigin origin,
        AudienceType audienceType, long? proposingCenterId,
        long? targetCenterId, bool managedByQuality)
    {
        if (isAdmin) return true;
        bool deanDestination = IsDeanDestination(origin, audienceType,
            proposingCenterId, targetCenterId, managedByQuality);
        if (!deanDestination) return isQuality;
        return isDean && proposingCenterId is long centerId &&
            deanCenterIds.Contains(centerId);
    }
}

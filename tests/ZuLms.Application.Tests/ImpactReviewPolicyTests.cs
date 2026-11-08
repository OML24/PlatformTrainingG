using ZuLms.Application.Features.Courses;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Tests;

public sealed class ImpactReviewPolicyTests
{
    [Fact]
    public void DeanProposalForSameFacultyIsRoutedToDean()
    {
        Assert.True(ImpactReviewPolicy.IsDeanDestination(
            CourseOrigin.DeanProposal, AudienceType.Faculty,
            proposingCenterId: 10, targetCenterId: 10,
            managedByQuality: false));
    }

    [Theory]
    [InlineData(CourseOrigin.QualityCreated, AudienceType.Faculty, 10L, 10L, false)]
    [InlineData(CourseOrigin.AdminCreated, AudienceType.Faculty, 10L, 10L, false)]
    [InlineData(CourseOrigin.DeanProposal, AudienceType.General, 10L, null, false)]
    [InlineData(CourseOrigin.DeanProposal, AudienceType.Administrative, 10L, null, false)]
    [InlineData(CourseOrigin.DeanProposal, AudienceType.Named, 10L, null, false)]
    [InlineData(CourseOrigin.DeanProposal, AudienceType.Faculty, 10L, 20L, false)]
    [InlineData(CourseOrigin.DeanProposal, AudienceType.Faculty, 10L, 10L, true)]
    [InlineData(CourseOrigin.DeanProposal, AudienceType.Faculty, null, 10L, false)]
    public void AllOtherRoutesAreRoutedToQuality(CourseOrigin origin,
        AudienceType audienceType, long? proposingCenterId,
        long? targetCenterId, bool managedByQuality)
    {
        Assert.False(ImpactReviewPolicy.IsDeanDestination(origin, audienceType,
            proposingCenterId, targetCenterId, managedByQuality));
    }

    [Fact]
    public void AdminCanReviewBothDestinations()
    {
        Assert.True(CanReview(isAdmin: true, isQuality: false, isDean: false,
            deanCenters: [], CourseOrigin.DeanProposal, AudienceType.Faculty,
            proposingCenterId: 10, targetCenterId: 10));
        Assert.True(CanReview(isAdmin: true, isQuality: false, isDean: false,
            deanCenters: [], CourseOrigin.QualityCreated, AudienceType.General,
            proposingCenterId: null, targetCenterId: null));
    }

    [Fact]
    public void QualityCanOnlyReviewQualityDestination()
    {
        Assert.True(CanReview(isAdmin: false, isQuality: true, isDean: false,
            deanCenters: [], CourseOrigin.QualityCreated, AudienceType.General,
            proposingCenterId: null, targetCenterId: null));
        Assert.False(CanReview(isAdmin: false, isQuality: true, isDean: false,
            deanCenters: [], CourseOrigin.DeanProposal, AudienceType.Faculty,
            proposingCenterId: 10, targetCenterId: 10));
    }

    [Fact]
    public void DeanCanOnlyReviewDeanDestinationForAssignedFaculty()
    {
        Assert.True(CanReview(isAdmin: false, isQuality: false, isDean: true,
            deanCenters: [10], CourseOrigin.DeanProposal, AudienceType.Faculty,
            proposingCenterId: 10, targetCenterId: 10));
        Assert.False(CanReview(isAdmin: false, isQuality: false, isDean: true,
            deanCenters: [20], CourseOrigin.DeanProposal, AudienceType.Faculty,
            proposingCenterId: 10, targetCenterId: 10));
        Assert.False(CanReview(isAdmin: false, isQuality: false, isDean: true,
            deanCenters: [10], CourseOrigin.QualityCreated, AudienceType.Faculty,
            proposingCenterId: 10, targetCenterId: 10));
    }

    [Fact]
    public void InstructorAndExternalInstructorDoNotGrantReview()
    {
        // Neither instructor role is an accepted reviewer input to the policy.
        Assert.False(CanReview(isAdmin: false, isQuality: false, isDean: false,
            deanCenters: [], CourseOrigin.QualityCreated, AudienceType.General,
            proposingCenterId: null, targetCenterId: null));
    }

    private static bool CanReview(bool isAdmin, bool isQuality, bool isDean,
        IReadOnlyCollection<long> deanCenters, CourseOrigin origin,
        AudienceType audienceType, long? proposingCenterId, long? targetCenterId) =>
        ImpactReviewPolicy.CanReview(isAdmin, isQuality, isDean, deanCenters,
            origin, audienceType, proposingCenterId, targetCenterId,
            managedByQuality: false);
}

using ZuLms.Application.Features.Courses;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Tests;

public sealed class ExternalInstructorCourseStatusTests
{
    [Theory]
    [InlineData(ExternalInstructorDecision.RequestNewAccount, CourseStatus.AwaitingAccount)]
    [InlineData(ExternalInstructorDecision.ExistingAccount, CourseStatus.Approved)]
    [InlineData(ExternalInstructorDecision.ManagedByQuality, CourseStatus.Approved)]
    public void ApprovalUsesExpectedCourseStatus(ExternalInstructorDecision decision, CourseStatus expected)
    {
        Assert.Equal(expected, ExternalInstructorCourseStatus.AfterApproval(decision));
    }

    [Fact]
    public void AccountCompletionActivatesAwaitingCourseAndPreservesLegacyApprovedStatus()
    {
        Assert.True(ExternalInstructorCourseStatus.CanCompleteAccountRequest(CourseStatus.AwaitingAccount));
        Assert.True(ExternalInstructorCourseStatus.CanCompleteAccountRequest(CourseStatus.Approved));
        Assert.False(ExternalInstructorCourseStatus.CanCompleteAccountRequest(CourseStatus.Proposed));
        Assert.Equal(CourseStatus.Approved,
            ExternalInstructorCourseStatus.AfterAccountCompletion(CourseStatus.AwaitingAccount));
        Assert.Equal(CourseStatus.Approved,
            ExternalInstructorCourseStatus.AfterAccountCompletion(CourseStatus.Approved));
    }
}

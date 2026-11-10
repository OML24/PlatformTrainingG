using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

public static class ExternalInstructorCourseStatus
{
    public static CourseStatus AfterApproval(ExternalInstructorDecision? decision) =>
        decision == ExternalInstructorDecision.RequestNewAccount
            ? CourseStatus.AwaitingAccount
            : CourseStatus.Approved;

    public static bool CanCompleteAccountRequest(CourseStatus status) =>
        status is CourseStatus.AwaitingAccount or CourseStatus.Approved;

    public static CourseStatus AfterAccountCompletion(CourseStatus status) =>
        status == CourseStatus.AwaitingAccount ? CourseStatus.Approved : status;
}

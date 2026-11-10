using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Rules;

public static class CourseStateMachine
{
    private static readonly IReadOnlyDictionary<CourseStatus, CourseStatus[]> Allowed =
        new Dictionary<CourseStatus, CourseStatus[]>
        {
            [CourseStatus.Proposed] = [CourseStatus.Approved, CourseStatus.AwaitingAccount, CourseStatus.Returned, CourseStatus.Rejected],
            [CourseStatus.Returned] = [CourseStatus.Proposed],
            [CourseStatus.Approved] = [CourseStatus.Finished, CourseStatus.Cancelled],
            [CourseStatus.AwaitingAccount] = [CourseStatus.Approved, CourseStatus.Returned, CourseStatus.Rejected, CourseStatus.Cancelled],
            [CourseStatus.Finished] = [CourseStatus.Approved]
        };

    public static bool CanTransition(CourseStatus from, CourseStatus to) => Next(from).Contains(to);

    public static IReadOnlyList<CourseStatus> Next(CourseStatus status) =>
        Allowed.TryGetValue(status, out var next) ? next : [];
}

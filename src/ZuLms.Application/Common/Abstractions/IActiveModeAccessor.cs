using ZuLms.Domain.Enums;

namespace ZuLms.Application.Common.Abstractions;

public interface IActiveModeAccessor
{
    RoleCode? SelectedMode { get; }
}

public static class ActiveModeResolver
{
    private static readonly RoleCode[] DefaultOrder =
    [RoleCode.Admin, RoleCode.Quality, RoleCode.Dean, RoleCode.CenterManager, RoleCode.Instructor, RoleCode.Member, RoleCode.ExternalInstructor];

    public static RoleCode? Resolve(IReadOnlySet<RoleCode> accountRoles, RoleCode? selected) =>
        selected is { } mode && accountRoles.Contains(mode)
            ? mode
            : DefaultOrder.Where(accountRoles.Contains).Select(mode => (RoleCode?)mode).FirstOrDefault();
}

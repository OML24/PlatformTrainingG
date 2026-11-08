using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Common.Security;

public static class ScopeGuard
{
    public static bool AllowsPersonalRead(EffectivePermissionSet permissions, string permissionKey) =>
        permissions.HasMode(RoleCode.Member) && permissions.Keys.Contains(permissionKey);

    public static bool AllowsDeanCenterRead(EffectivePermissionSet permissions,
        string permissionKey, long centerId) =>
        permissions.HasMode(RoleCode.Dean) && permissions.Keys.Contains(permissionKey) &&
        permissions.DeanScopeCenterIds.Contains(centerId);

    public static bool AllowsQualityManagedCourse(EffectivePermissionSet permissions, bool managedByQuality) =>
        permissions.HasMode(RoleCode.Admin) ||
        permissions.HasMode(RoleCode.Quality) && managedByQuality;

    public static bool Allows(
        EffectivePermissionSet permissions, PermissionScope scope,
        long? resourceUserId = null, long? centerId = null,
        long? instructorUserId = null, bool managedByQuality = false)
    {
        if (permissions.ActiveMode is not { } mode || !permissions.Roles.Contains(mode)) return false;
        if (permissions.HasMode(RoleCode.Admin)) return true;
        return scope switch
        {
            PermissionScope.Global => true,
            PermissionScope.Self => resourceUserId == permissions.UserId,
            PermissionScope.OwnFaculty => centerId.HasValue && permissions.DeanScopeCenterIds.Contains(centerId.Value),
            PermissionScope.InstructedCourse =>
                (instructorUserId == permissions.UserId &&
                    (permissions.HasMode(RoleCode.Instructor) || permissions.HasMode(RoleCode.ExternalInstructor))) ||
                (managedByQuality && permissions.HasMode(RoleCode.Quality)),
            _ => false
        };
    }
}

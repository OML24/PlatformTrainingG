using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Security;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Tests;

public sealed class ScopeGuardTests
{
    private static EffectivePermissionSet Set(RoleCode mode, params RoleCode[] roles) => new(
        7, mode, roles.ToHashSet(), new HashSet<string>(), new HashSet<long> { 10 });

    [Fact]
    public void InstructorOwnershipRequiresInstructorMode()
    {
        Assert.True(ScopeGuard.Allows(Set(RoleCode.Instructor, RoleCode.Instructor),
            PermissionScope.InstructedCourse, instructorUserId: 7));
        Assert.False(ScopeGuard.Allows(Set(RoleCode.Quality, RoleCode.Quality),
            PermissionScope.InstructedCourse, instructorUserId: 7));
    }

    [Fact]
    public void QualityOnlyManagesQualityCoursesAndAdminBypassIsModeScoped()
    {
        Assert.True(ScopeGuard.Allows(Set(RoleCode.Quality, RoleCode.Quality),
            PermissionScope.InstructedCourse, managedByQuality: true));
        Assert.False(ScopeGuard.Allows(Set(RoleCode.Instructor, RoleCode.Instructor),
            PermissionScope.InstructedCourse, managedByQuality: true));
        Assert.False(ScopeGuard.AllowsQualityManagedCourse(Set(RoleCode.Quality, RoleCode.Quality), false));
        Assert.True(ScopeGuard.AllowsQualityManagedCourse(Set(RoleCode.Quality, RoleCode.Quality), true));
        Assert.False(ScopeGuard.Allows(Set(RoleCode.Member, RoleCode.Admin, RoleCode.Member),
            PermissionScope.InstructedCourse));
        Assert.True(ScopeGuard.Allows(Set(RoleCode.Admin, RoleCode.Admin), PermissionScope.Global));
    }

    [Fact]
    public void MissingModeCannotUseAdminBypass()
    {
        var noRoles = new EffectivePermissionSet(7, null, new HashSet<RoleCode>(),
            new HashSet<string>(), new HashSet<long>());
        Assert.False(ScopeGuard.Allows(noRoles, PermissionScope.Global));
        Assert.False(ScopeGuard.Allows(noRoles, PermissionScope.InstructedCourse,
            instructorUserId: 7, managedByQuality: true));
    }

    [Fact]
    public void PersonalAndDeanCenterReadsRequireActiveModeKeyAndScope()
    {
        var memberKeyInQualityMode = new EffectivePermissionSet(7, RoleCode.Quality,
            new HashSet<RoleCode> { RoleCode.Quality, RoleCode.Member },
            new HashSet<string> { "certificates.view.own" }, new HashSet<long>());
        Assert.False(ScopeGuard.AllowsPersonalRead(memberKeyInQualityMode,
            "certificates.view.own"));

        var member = new EffectivePermissionSet(7, RoleCode.Member,
            new HashSet<RoleCode> { RoleCode.Member },
            new HashSet<string> { "impact.submit" }, new HashSet<long>());
        Assert.True(ScopeGuard.AllowsPersonalRead(member, "impact.submit"));
        Assert.False(ScopeGuard.AllowsPersonalRead(member, "procert.download.own"));

        var dean = new EffectivePermissionSet(7, RoleCode.Dean,
            new HashSet<RoleCode> { RoleCode.Dean },
            new HashSet<string> { "need.submit" }, new HashSet<long> { 12 });
        Assert.True(ScopeGuard.AllowsDeanCenterRead(dean, "need.submit", 12));
        Assert.False(ScopeGuard.AllowsDeanCenterRead(dean, "need.submit", 13));
        Assert.False(ScopeGuard.AllowsDeanCenterRead(member, "need.submit", 12));
    }

    [Fact]
    public void CenterManagerActsLikeDeanOnlyOverOwnCenter()
    {
        var manager = new EffectivePermissionSet(7, RoleCode.CenterManager,
            new HashSet<RoleCode> { RoleCode.CenterManager },
            new HashSet<string> { "need.submit" }, new HashSet<long> { 40 });
        Assert.True(manager.IsUnitHead);
        Assert.True(ScopeGuard.AllowsDeanCenterRead(manager, "need.submit", 40));
        Assert.False(ScopeGuard.AllowsDeanCenterRead(manager, "need.submit", 12));
        Assert.True(ScopeGuard.Allows(manager, PermissionScope.OwnFaculty, centerId: 40));
        Assert.False(ScopeGuard.Allows(manager, PermissionScope.OwnFaculty, centerId: 12));
    }

    [Fact]
    public void CenterManagerOwnsTheDeanWorkspaceKeys()
    {
        var keys = Features.Permissions.PermissionSetRules.ForMode(RoleCode.CenterManager,
            ["page.dean.propose", "course.propose", "need.submit", "impact.review", "page.admin.users"],
            [], AccountType.Employee);
        Assert.Contains("page.dean.propose", keys);
        Assert.Contains("course.propose", keys);
        Assert.Contains("need.submit", keys);
        Assert.Contains("impact.review", keys);
        Assert.DoesNotContain("page.admin.users", keys);
    }
}

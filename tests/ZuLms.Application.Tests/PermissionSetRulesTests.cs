using ZuLms.Application.Features.Permissions;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Tests;

public sealed class PermissionSetRulesTests
{
    [Fact]
    public void ExternalInstructorCannotInheritMemberOrSharedPages()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            "page.instructor.courses", "page.shared.directory", "page.shared.profile",
            "page.member.teaching", "lecture.manage", "attendance.enter_code",
            "participants.self_register"
        };
        PermissionSetRules.ApplyAccountType(AccountType.ExternalInstructor, keys);
        Assert.Equal(2, keys.Count);
        Assert.Contains("page.instructor.courses", keys);
        Assert.Contains("lecture.manage", keys);
    }

    [Fact]
    public void ModeOwnsRoleAndOverridePermissionsAndDenyWins()
    {
        var keys = PermissionSetRules.ForMode(RoleCode.Quality,
            ["page.quality.courses", "page.member.dashboard", "quality.custom", "page.admin.users"],
            [("page.member.dashboard", OverrideMode.Allow), ("quality.custom", OverrideMode.Deny),
             ("page.instructor.courses", OverrideMode.Allow)], AccountType.Employee);
        Assert.Contains("page.quality.courses", keys);
        Assert.Contains("page.instructor.courses", keys);
        Assert.DoesNotContain("quality.custom", keys);
        Assert.DoesNotContain("page.member.dashboard", keys);
        Assert.DoesNotContain("page.admin.users", keys);
    }

    [Fact]
    public void MemberAndDeanModeStayWithinOwnedKeysAndScopeIsDeanOnly()
    {
        var member = PermissionSetRules.ForMode(RoleCode.Member,
            ["page.member.dashboard", "evaluation.submit", "page.quality.courses"],
            [("page.quality.courses", OverrideMode.Allow)], AccountType.Employee);
        Assert.Contains("evaluation.submit", member);
        Assert.DoesNotContain("page.quality.courses", member);
        Assert.DoesNotContain("page.quality.courses", PermissionSetRules.ForMode(RoleCode.Dean,
            [], [("page.quality.courses", OverrideMode.Allow)], AccountType.Employee));
    }

    [Fact]
    public void InstructorCannotGetImpactOrCertificateScreensButQualityCan()
    {
        var instructor = PermissionSetRules.ForMode(RoleCode.ExternalInstructor,
            ["page.instructor.courses", "page.instructor.impact", "page.instructor.certificates"],
            [], AccountType.Employee);
        Assert.Contains("page.instructor.courses", instructor);
        Assert.DoesNotContain("page.instructor.impact", instructor);
        Assert.DoesNotContain("page.instructor.certificates", instructor);
        var quality = PermissionSetRules.ForMode(RoleCode.Quality,
            ["page.instructor.impact", "page.instructor.certificates"], [], AccountType.Employee);
        Assert.Contains("page.instructor.impact", quality);
        Assert.Contains("page.instructor.certificates", quality);
    }

    [Fact]
    public void CatalogOperationOwnershipSurvivesFilteringForEveryWorkspace()
    {
        AssertOwned(RoleCode.Admin, "user.roles.assign", "settings.edit", "credentials.reveal");
        AssertOwned(RoleCode.Quality, "course.edit", "proposal.decide");
        AssertOwned(RoleCode.Dean, "course.propose", "need.submit");
        AssertOwned(RoleCode.Instructor, "lecture.manage", "attendance.activate_code", "exam.manage", "course.finish");
        AssertOwned(RoleCode.ExternalInstructor, "lecture.manage", "attendance.activate_code", "exam.manage", "course.finish");
        AssertOwned(RoleCode.Member, "participants.self_register", "attendance.enter_code", "evaluation.submit");
    }

    [Fact]
    public void AllowOverridesCannotCrossExclusiveCatalogOwnership()
    {
        Assert.Empty(PermissionSetRules.ForMode(RoleCode.Quality, [],
            [("user.roles.assign", OverrideMode.Allow)], AccountType.Employee));
        Assert.Empty(PermissionSetRules.ForMode(RoleCode.Member, [],
            [("course.propose", OverrideMode.Allow)], AccountType.Employee));
        Assert.Empty(PermissionSetRules.ForMode(RoleCode.Dean, [],
            [("course.cancel_direct", OverrideMode.Allow)], AccountType.Employee));
    }

    [Fact]
    public void PersonalMemberPagesAndActionsCannotBeAllowedFromOtherModes()
    {
        foreach (var mode in new[] { RoleCode.Admin, RoleCode.Quality, RoleCode.Dean, RoleCode.Instructor })
        {
            var keys = PermissionSetRules.ForMode(mode, [],
                [("page.member.dashboard", OverrideMode.Allow), ("participants.self_register", OverrideMode.Allow),
                 ("attendance.enter_code", OverrideMode.Allow)], AccountType.Employee);
            Assert.Empty(keys);
        }
        Assert.Contains("page.member.dashboard", PermissionSetRules.ForMode(RoleCode.Member,
            [], [("page.member.dashboard", OverrideMode.Allow)], AccountType.Employee));
    }

    private static void AssertOwned(RoleCode mode, params string[] keys)
    {
        var effective = PermissionSetRules.ForMode(mode, keys, [], AccountType.Employee);
        Assert.Equal(keys.ToHashSet(StringComparer.Ordinal), effective);
    }
}

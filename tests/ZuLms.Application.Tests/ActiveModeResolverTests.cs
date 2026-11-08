using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Tests;

public sealed class ActiveModeResolverTests
{
    [Fact]
    public void ValidSelectionWinsAndInvalidSelectionUsesSharedPriority()
    {
        var roles = new HashSet<RoleCode> { RoleCode.Member, RoleCode.Quality, RoleCode.Admin };
        Assert.Equal(RoleCode.Member, ActiveModeResolver.Resolve(roles, RoleCode.Member));
        Assert.Equal(RoleCode.Admin, ActiveModeResolver.Resolve(roles, RoleCode.Dean));
    }

    [Fact]
    public void InstructorPrioritizesBeforeMemberWhenSelectionMissing()
    {
        var roles = new HashSet<RoleCode> { RoleCode.ExternalInstructor, RoleCode.Member };
        Assert.Equal(RoleCode.Member, ActiveModeResolver.Resolve(roles, null));
        roles.Add(RoleCode.Instructor);
        Assert.Equal(RoleCode.Instructor, ActiveModeResolver.Resolve(roles, null));
    }

    [Fact]
    public void AccountWithoutRolesHasNoActiveMode()
    {
        Assert.Null(ActiveModeResolver.Resolve(new HashSet<RoleCode>(), null));
        Assert.Null(ActiveModeResolver.Resolve(new HashSet<RoleCode>(), RoleCode.Admin));
    }
}

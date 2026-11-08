using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ZuLms.Application.Common.Abstractions;

namespace ZuLms.Web.Security;

public sealed class PermissionAuthorizationHandler(IPermissionService permissions, IHttpContextAccessor contextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId)) return;
        CancellationToken cancellationToken = contextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        foreach (string key in requirement.Keys)
        {
            if (await permissions.HasAsync(userId, key, cancellationToken))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}

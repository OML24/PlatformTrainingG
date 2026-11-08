using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Permissions;
using ZuLms.Domain.Enums;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/roles")]
[RequirePermission("page.admin.roles")]
public sealed class RolesController(IRolePermissionAdminService roles) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await roles.GetAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Forbid();
    }

    [HttpPut("{role}/permissions")]
    public async Task<IActionResult> Update(RoleCode role, UpdateRolePermissionsRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await roles.UpdateAsync(actor, role, request.Keys, ct);
        if (result.Ok) return NoContent();
        return result.Error?.Code == "FORBIDDEN" ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    public sealed record UpdateRolePermissionsRequest(IReadOnlyCollection<string> Keys);
}

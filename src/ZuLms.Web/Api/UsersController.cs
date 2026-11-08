using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Users;
using ZuLms.Domain.Enums;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(IUserService users) : ControllerBase
{
    [HttpGet("faculties")]
    [RequirePermission("user.roles.assign", "page.shared.directory")]
    public async Task<IActionResult> Faculties(CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await users.ListFacultiesAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("{id:long}/permissions")]
    [RequirePermission("user.permissions.override")]
    public async Task<IActionResult> Permissions(long id, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await users.ListPermissionsAsync(actor, id, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("credentials-directory")]
    [RequirePermission("page.admin.credentials")]
    public async Task<IActionResult> CredentialsDirectory([FromQuery] string? search,
        [FromQuery] CredentialsDirectoryFilter filter = CredentialsDirectoryFilter.All,
        [FromQuery] AccountType? accountType = null, [FromQuery] RoleCode? role = null,
        [FromQuery] long? centerId = null, [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await users.SearchCredentialsAsync(actor, search, filter,
            accountType, role, centerId, isActive, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost("local")]
    [RequirePermission("user.create")]
    public async Task<IActionResult> CreateLocal(CreateLocalRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await users.CreateLocalAsync(actor, request.LoginName, request.FullNameAr,
            request.FullNameEn, request.Password, request.PreferredLanguage, ct);
        return result.Ok ? Created($"/api/v1/users/{result.Value}", new { id = result.Value }) : Error(result);
    }

    [HttpGet("{id:long}")]
    [RequirePermission("page.admin.credentials")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await users.GetDetailsAsync(actor, id, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPut("{id:long}")]
    [RequirePermission("user.edit")]
    public async Task<IActionResult> Edit(long id, EditUserRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await users.EditAsync(actor, id, request.ExpectedVersion, request.FullNameEn,
            request.EmailOverride, request.PreferredLanguage, request.AccessOverrideAllow,
            request.AccessOverrideReason, ct));
    }

    [HttpPost("{id:long}/deactivate")]
    [RequirePermission("user.deactivate")]
    public async Task<IActionResult> Deactivate(long id, DeactivateUserRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await users.DeactivateAsync(actor, id, request.ExpectedVersion, request.Reason, ct));
    }

    [HttpPost("{id:long}/restore")]
    [RequirePermission("user.restore")]
    public async Task<IActionResult> Restore(long id, VersionRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await users.RestoreAsync(actor, id, request.ExpectedVersion, ct));
    }

    [HttpPut("{id:long}/roles")]
    [RequirePermission("user.roles.assign")]
    public async Task<IActionResult> AssignRole(long id, AssignRoleRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await users.AssignRoleAsync(actor, id, request.Role, request.ScopeCenterId,
            request.Enabled, request.ExpectedVersion, ct));
    }

    [HttpPut("{id:long}/permissions/{key}")]
    [RequirePermission("user.permissions.override")]
    public async Task<IActionResult> SetPermission(long id, string key,
        SetPermissionRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await users.SetPermissionOverrideAsync(actor, id, key,
            request.Mode, request.ExpectedVersion, ct));
    }

    private bool TryActor(out long actor) => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);

    private IActionResult Map(Result result) => result.Ok ? NoContent() : Error(result);

    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "USER_NOT_FOUND" => NotFound(new { code }),
            "CONFLICT" or "LAST_ADMIN" or "LAST_ADMIN_GUARD" or "LOGIN_EXISTS" => Conflict(new { code }),
            _ => BadRequest(new { code })
        };
    }

    public sealed record DeactivateUserRequest(uint ExpectedVersion, string Reason);
    public sealed record VersionRequest(uint ExpectedVersion);
    public sealed record AssignRoleRequest(uint ExpectedVersion, RoleCode Role,
        long? ScopeCenterId, bool Enabled);
    public sealed record SetPermissionRequest(uint ExpectedVersion, OverrideMode? Mode);
    public sealed record EditUserRequest(uint ExpectedVersion, string? FullNameEn,
        string? EmailOverride, UiLanguage PreferredLanguage, bool AccessOverrideAllow,
        string? AccessOverrideReason);
    public sealed record CreateLocalRequest(string LoginName, string FullNameAr,
        string? FullNameEn, string Password, UiLanguage PreferredLanguage);
}

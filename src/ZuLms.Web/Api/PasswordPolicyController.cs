using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Settings;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/password-policy")]
public sealed class PasswordPolicyController(IPasswordPolicyService policies) : ControllerBase
{
    [HttpGet]
    [RequirePermission("page.admin.password_policy")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await policies.GetAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPut]
    [RequirePermission("password_policy.edit")]
    public async Task<IActionResult> Update(PasswordPolicyDto request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await policies.UpdateAsync(actor, request, ct));
    }

    [HttpGet("blocked")]
    [RequirePermission("page.admin.password_policy")]
    public async Task<IActionResult> ListBlocked([FromQuery] string? search, [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await policies.ListBlockedAsync(actor, search, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost("blocked")]
    [RequirePermission("password_policy.edit")]
    public async Task<IActionResult> AddBlocked(AddBlockedRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await policies.AddBlockedAsync(actor, request.Password, ct));
    }

    [HttpDelete("blocked/{id:long}")]
    [RequirePermission("password_policy.edit")]
    public async Task<IActionResult> RemoveBlocked(long id, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await policies.RemoveBlockedAsync(actor, id, ct));
    }

    private bool TryActor(out long actor) => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Map(Result result) => result.Ok ? NoContent() : Error(result);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "BLOCKED_PASSWORD_NOT_FOUND" => NotFound(new { code }),
            "BLOCKED_PASSWORD_EXISTS" => Conflict(new { code }),
            _ => BadRequest(new { code })
        };
    }

    public sealed record AddBlockedRequest(string Password);
}

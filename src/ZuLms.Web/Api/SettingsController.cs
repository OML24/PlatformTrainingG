using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/settings")]
public sealed class SettingsController(ISettingsService settings) : ControllerBase
{
    [HttpGet]
    [RequirePermission("page.admin.settings")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await settings.ListAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPut("{key}")]
    [RequirePermission("settings.edit")]
    public async Task<IActionResult> Update(string key, UpdateSettingRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await settings.SetAsync(key, request.Value, request.ExpectedVersion, actor, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    private bool TryActor(out long actor) => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "SETTING_NOT_FOUND" => NotFound(new { code }),
            "CONFLICT" => Conflict(new { code }),
            _ => BadRequest(new { code })
        };
    }

    public sealed record UpdateSettingRequest(string Value, uint ExpectedVersion);
}

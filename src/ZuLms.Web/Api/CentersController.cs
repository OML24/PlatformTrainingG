using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Sync;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/centers")]
public sealed class CentersController(ICenterAdminService centers) : ControllerBase
{
    [HttpGet]
    [RequirePermission("page.admin.centers")]
    public async Task<IActionResult> List([FromQuery] string? search,
        [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await centers.ListAsync(actor, search, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPut("{id:long}")]
    [RequirePermission("centers.edit")]
    public async Task<IActionResult> Update(long id, UpdateCenterRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await centers.UpdateAsync(actor, id, request.ExpectedVersion,
            request.IsFaculty, request.ShortCode, request.IsActive, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    private bool TryActor(out long actor) => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "CENTER_NOT_FOUND" => NotFound(new { code }),
            "CONFLICT" => Conflict(new { code }),
            _ => BadRequest(new { code })
        };
    }

    public sealed record UpdateCenterRequest(uint ExpectedVersion, bool IsFaculty,
        string? ShortCode, bool IsActive);
}

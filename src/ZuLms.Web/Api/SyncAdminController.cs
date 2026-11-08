using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Sync;
using ZuLms.Domain.Enums;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/sync")]
public sealed class SyncAdminController(IUniversitySyncAdminService admin) : ControllerBase
{
    [HttpGet("sides")]
    [RequirePermission("page.admin.sync")]
    public async Task<IActionResult> Sides(CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await admin.ListSidesAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPut("sides/{sideCode:int}/imported")]
    [RequirePermission("sync.run")]
    public async Task<IActionResult> SetSide(int sideCode, SetSideRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await admin.SetSideImportedAsync(actor, sideCode, request.Imported, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpGet("runs")]
    [RequirePermission("page.admin.sync")]
    public async Task<IActionResult> Runs(CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await admin.ListRunsAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("reviews")]
    [RequirePermission("page.admin.sync")]
    public async Task<IActionResult> Reviews([FromQuery] SyncReviewReason? reason,
        [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await admin.ListReviewsAsync(actor, reason, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost("reviews/{id:long}/resolve")]
    [RequirePermission("sync.review.resolve")]
    public async Task<IActionResult> Resolve(long id, ResolveReviewRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await admin.ResolveReviewAsync(actor, id, request.Resolution, request.Notes, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    private bool TryActor(out long actor) => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "SIDE_NOT_FOUND" or "REVIEW_NOT_FOUND" => NotFound(new { code }),
            "REVIEW_ALREADY_RESOLVED" => Conflict(new { code }),
            _ => BadRequest(new { code })
        };
    }

    public sealed record SetSideRequest(bool Imported);
    public sealed record ResolveReviewRequest(SyncReviewResolution Resolution, string? Notes);
}

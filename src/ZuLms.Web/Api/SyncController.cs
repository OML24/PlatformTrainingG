using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Sync;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/sync/runs")]
public sealed class SyncController(IUniversitySyncService sync) : ControllerBase
{
    [HttpPost]
    [RequirePermission("sync.run")]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await sync.StartAsync(actor, ct);
        if (result.Ok) return Accepted($"/api/v1/sync/runs/{result.Value}", new { id = result.Value });
        return Error(result);
    }

    [HttpGet("{id:long}")]
    [RequirePermission("page.admin.sync")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await sync.GetRunAsync(actor, id, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    private bool TryActor(out long actor) => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "SYNC_RUN_NOT_FOUND" => NotFound(new { code }),
            "SYNC_ALREADY_RUNNING" => Conflict(new { code }),
            _ => BadRequest(new { code })
        };
    }
}

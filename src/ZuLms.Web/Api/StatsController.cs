using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Features.Stats;
using ZuLms.Domain.Enums;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/stats")]
public sealed class StatsController(IStatsService stats) : ControllerBase
{
    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups([FromQuery] RoleCode role,
        [FromQuery] string? userSearch, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await stats.LookupsAsync(actor, role, userSearch, ct);
        return result.Ok ? Ok(result.Value) : Forbid();
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] RoleCode role,
        [FromQuery] long? centerId,
        [FromQuery] long? userId, [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await stats.DashboardAsync(actor,
            new(role, centerId, userId, fromDate, toDate), ct);
        if (result.Ok) return Ok(result.Value);
        return result.Error?.Code == "FORBIDDEN" ? Forbid() :
            BadRequest(new { code = result.Error?.Code });
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
}

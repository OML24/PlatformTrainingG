using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/evaluation")]
public sealed class EvaluationAggregateController(
    IEvaluationAggregateService aggregate) : ControllerBase
{
    [HttpGet("aggregate")]
    [RequirePermission("evaluation.results.all")]
    public async Task<IActionResult> Get([FromQuery] long? courseId,
        [FromQuery] long? centerId, [FromQuery] long? trackId,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] long? versionId, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await aggregate.GetAsync(actor,
            new(courseId, centerId, trackId, fromDate, toDate, versionId), ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("versions")]
    [RequirePermission("evaluation.results.all")]
    public async Task<IActionResult> Versions(CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await aggregate.VersionsAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result) => result.Error?.Code == "FORBIDDEN"
        ? Forbid() : BadRequest(new { code = result.Error?.Code });
}

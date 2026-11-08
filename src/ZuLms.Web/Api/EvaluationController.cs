using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

/// <summary>Quality/admin evaluation results and evaluation-period extension.</summary>
[ApiController]
[Authorize]
[Route("api/v1/evaluation")]
public sealed class EvaluationResultsController(IEvaluationQueryService queries)
    : ControllerBase
{
    [HttpGet("results/{courseId:long}")]
    [RequirePermission("evaluation.results.all", "evaluation.results.course")]
    public async Task<IActionResult> Results(long courseId, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await queries.CourseResultsAsync(actor, courseId, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPut("period/{courseId:long}")]
    [RequirePermission("evaluation.extend_period")]
    public async Task<IActionResult> ExtendPeriod(long courseId,
        ExtendPeriodRequest request, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await queries.ExtendPeriodAsync(actor, courseId,
            request.NewEndDate, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "COURSE_NOT_FOUND" => NotFound(new { code }),
            _ => BadRequest(new { code })
        };
    }
}

public sealed record ExtendPeriodRequest(DateOnly NewEndDate);

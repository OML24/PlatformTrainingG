using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/courses/{courseId:long}/evaluation")]
public sealed class EvaluationsController(IEvaluationService evaluations)
    : ControllerBase
{
    [HttpGet]
    [RequirePermission("evaluation.submit")]
    public async Task<IActionResult> Form(long courseId, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await evaluations.GetFormAsync(actor, courseId, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost]
    [RequirePermission("evaluation.submit")]
    public async Task<IActionResult> Submit(long courseId,
        SubmitEvaluationRequest request, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await evaluations.SubmitAsync(actor, courseId,
            request.Answers, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result) => result.Error?.Code == "FORBIDDEN"
        ? Forbid() : BadRequest(new { code = result.Error?.Code });
}

public sealed record SubmitEvaluationRequest(
    IReadOnlyList<EvaluationAnswerInput> Answers);

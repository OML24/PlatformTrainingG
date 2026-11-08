using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/cancellations")]
public sealed class CourseCancellationController(ICourseCancellationService cancellations)
    : ControllerBase
{
    [HttpGet("pending")]
    [RequirePermission("cancellation.decide")]
    public async Task<IActionResult> Pending(CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await cancellations.PendingAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Forbid();
    }

    [HttpPost("{id:long}/decision")]
    [RequirePermission("cancellation.decide")]
    public async Task<IActionResult> Decide(long id, CancellationDecisionRequest input,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await cancellations.DecideAsync(actor, id, input.Approve,
            input.Notes, ct);
        return Respond(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);

    private IActionResult Respond(Result result) => result.Ok ? NoContent() :
        result.Error?.Code == "FORBIDDEN" ? Forbid() :
        BadRequest(new { code = result.Error?.Code });
}

[ApiController]
[Route("api/v1/courses/{courseId:long}/cancellation")]
public sealed class CourseCancellationActionController(ICourseCancellationService cancellations)
    : ControllerBase
{
    [HttpPost("request")]
    [RequirePermission("cancellation.request")]
    public async Task<IActionResult> SubmitRequest(long courseId,
        CancellationRequestInput input, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await cancellations.RequestAsync(actor, courseId,
            input.Reason, input.LetterFileId, ct);
        return result.Ok ? Created($"/api/v1/cancellations/{result.Value}",
            new { id = result.Value }) : Respond(result);
    }

    [HttpPost("direct")]
    [RequirePermission("course.cancel_direct")]
    public async Task<IActionResult> Direct(long courseId,
        DirectCancellationInput input, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await cancellations.CancelDirectAsync(actor, courseId,
            input.Reason, input.LetterFileId, ct);
        return Respond(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);

    private IActionResult Respond(Result result) => result.Ok ? NoContent() :
        result.Error?.Code == "FORBIDDEN" ? Forbid() :
        BadRequest(new { code = result.Error?.Code });
}

public sealed record CancellationDecisionRequest(bool Approve, string? Notes);
public sealed record CancellationRequestInput(string Reason, long? LetterFileId);
public sealed record DirectCancellationInput(string Reason, long LetterFileId);

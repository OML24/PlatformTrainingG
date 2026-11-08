using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Domain.Enums;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/courses/{courseId:long}/impact")]
public sealed class ImpactsController(IImpactService impacts) : ControllerBase
{
    [HttpGet("/api/v1/impact/review-courses")]
    [RequirePermission("impact.review")]
    public async Task<IActionResult> ReviewCourses(CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await impacts.ReviewCoursesAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet]
    [RequirePermission("impact.submit")]
    public async Task<IActionResult> Mine(long courseId, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await impacts.MineAsync(actor, courseId, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost]
    [RequirePermission("impact.submit")]
    public async Task<IActionResult> Submit(long courseId,
        SubmitImpactRequest request, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await impacts.SubmitAsync(actor, courseId,
            request.Type, request.FileId, ct);
        return result.Ok ? Created($"/api/v1/courses/{courseId}/impact/{result.Value}",
            new { id = result.Value }) : Error(result);
    }

    [HttpGet("pending")]
    [RequirePermission("impact.review")]
    public async Task<IActionResult> Pending(long courseId, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await impacts.PendingAsync(actor, courseId, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost("{submissionId:long}/review")]
    [RequirePermission("impact.review")]
    public async Task<IActionResult> Review(long courseId, long submissionId,
        ReviewImpactRequest request, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await impacts.ReviewAsync(actor, courseId, submissionId,
            request.Approve, request.Notes, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result) => result.Error?.Code == "FORBIDDEN"
        ? Forbid() : BadRequest(new { code = result.Error?.Code });
}

public sealed record SubmitImpactRequest(ImpactType Type, long FileId);
public sealed record ReviewImpactRequest(bool Approve, string? Notes);

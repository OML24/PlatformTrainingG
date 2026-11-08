using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/courses/{id:long}/admin")]
public sealed class AdminCourseLifecycleController(IAdminCourseLifecycleService lifecycle)
    : ControllerBase
{
    [HttpGet("deactivation-preview")]
    [RequirePermission("course.deactivate")]
    public async Task<IActionResult> Preview(long id, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await lifecycle.PreviewDeactivationAsync(actor, id, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost("deactivate")]
    [RequirePermission("course.deactivate")]
    public async Task<IActionResult> Deactivate(long id,
        DeactivateCourseRequest input, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        return Respond(await lifecycle.DeactivateAsync(actor, id,
            input.ExpectedVersion, input.Reason, input.CertificateAction, ct));
    }

    [HttpPost("restore")]
    [RequirePermission("course.restore")]
    public async Task<IActionResult> Restore(long id,
        CourseVersionRequest input, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        return Respond(await lifecycle.RestoreAsync(actor, id,
            input.ExpectedVersion, ct));
    }

    [HttpPost("archive")]
    [RequirePermission("course.archive")]
    public async Task<IActionResult> Archive(long id,
        CourseVersionRequest input, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        return Respond(await lifecycle.ArchiveAsync(actor, id,
            input.ExpectedVersion, true, ct));
    }

    [HttpPost("unarchive")]
    [RequirePermission("course.unarchive")]
    public async Task<IActionResult> Unarchive(long id,
        CourseVersionRequest input, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        return Respond(await lifecycle.ArchiveAsync(actor, id,
            input.ExpectedVersion, false, ct));
    }

    [HttpPost("reopen")]
    [RequirePermission("course.reopen")]
    public async Task<IActionResult> Reopen(long id,
        ReopenCourseRequest input, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        return Respond(await lifecycle.ReopenAsync(actor, id,
            input.ExpectedVersion, input.Confirmed, ct));
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Respond(Result result) => result.Ok ? NoContent() : Error(result);
    private IActionResult Error(Result result) => result.Error?.Code == "FORBIDDEN"
        ? Forbid() : BadRequest(new { code = result.Error?.Code });
}

public sealed record CourseVersionRequest(uint ExpectedVersion);
public sealed record DeactivateCourseRequest(uint ExpectedVersion, string Reason,
    CertificateDeactivationAction? CertificateAction);
public sealed record ReopenCourseRequest(uint ExpectedVersion, bool Confirmed);

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/lectures/{lectureId:long}/materials")]
public sealed class LectureMaterialsController(ILectureMaterialService materials)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(long lectureId, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await materials.ListAsync(actor, lectureId, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost]
    [RequirePermission("lecture.manage")]
    public async Task<IActionResult> Attach(long lectureId,
        AttachLectureMaterialRequest request, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await materials.AttachAsync(actor, lectureId,
            request.FileId, request.Title, ct);
        return result.Ok ? Created($"/api/v1/lectures/{lectureId}/materials/{result.Value}",
            new { id = result.Value }) : Error(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result) => result.Error?.Code == "FORBIDDEN"
        ? Forbid() : BadRequest(new { code = result.Error?.Code });
}

public sealed record AttachLectureMaterialRequest(long FileId, string? Title);

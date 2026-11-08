using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/courses/{courseId:long}/progress")]
public sealed class CourseProgressController(ICourseProgressService progress)
    : ControllerBase
{
    [HttpGet]
    [RequirePermission("page.member.enrolled")]
    public async Task<IActionResult> Mine(long courseId, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),
            out long actor)) return Unauthorized();
        var result = await progress.GetMineAsync(actor, courseId, ct);
        return result.Ok ? Ok(result.Value) : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }

    [HttpGet("roster")]
    [RequirePermission("page.instructor.participants")]
    public async Task<IActionResult> Roster(long courseId, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),
            out long actor)) return Unauthorized();
        var result = await progress.GetRosterAsync(actor, courseId, ct);
        return result.Ok ? Ok(result.Value) : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }
}

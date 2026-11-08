using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/courses/{courseId:long}/finish")]
public sealed class CourseFinishController(ICourseFinishService finish)
    : ControllerBase
{
    [HttpPost]
    [RequirePermission("course.finish")]
    public async Task<IActionResult> Post(long courseId, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),
            out long actor)) return Unauthorized();
        var result = await finish.FinishAsync(actor, courseId, ct);
        return result.Ok ? Ok(new { finalLecturesCount = result.Value }) :
            result.Error?.Code == "FORBIDDEN" ? Forbid() :
            BadRequest(new { code = result.Error?.Code });
    }
}

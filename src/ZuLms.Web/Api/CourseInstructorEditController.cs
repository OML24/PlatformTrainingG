using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/courses/{id:long}/instructor")]
public sealed class CourseInstructorEditController(ICourseInstructorEditService service)
    : ControllerBase
{
    [HttpGet("search")]
    [RequirePermission("course.edit")]
    public async Task<IActionResult> Search(long id, [FromQuery] bool external,
        [FromQuery] string? search, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await service.SearchAsync(actor, id, external, search, ct);
        return result.Ok ? Ok(result.Value) : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }

    [HttpPut]
    [RequirePermission("course.edit")]
    public async Task<IActionResult> Edit(long id, CourseInstructorEditInput input,
        CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await service.EditAsync(actor, id, input, ct);
        return result.Ok ? NoContent() : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }
}

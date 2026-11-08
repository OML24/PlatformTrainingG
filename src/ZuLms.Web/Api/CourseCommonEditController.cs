using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/courses/{id:long}/details")]
public sealed class CourseCommonEditController(ICourseCommonEditService service) : ControllerBase
{
    [HttpPut]
    [RequirePermission("course.edit")]
    public async Task<IActionResult> Edit(long id, CourseCommonEditInput input,
        CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await service.EditAsync(actor, id, input, ct);
        return result.Ok ? NoContent() : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }
}

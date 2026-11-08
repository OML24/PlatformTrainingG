using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/courses/{id:long}/attendance-threshold")]
public sealed class CourseAttendanceThresholdController(
    ICourseAttendanceThresholdService service) : ControllerBase
{
    [HttpPut]
    [RequirePermission("course.edit")]
    public async Task<IActionResult> Change(long id,
        ChangeAttendanceThresholdRequest input, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await service.ChangeAsync(actor, id, input.ExpectedVersion,
            input.Percent, ct);
        return result.Ok ? NoContent() : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }
}

public sealed record ChangeAttendanceThresholdRequest(uint ExpectedVersion,
    short? Percent);

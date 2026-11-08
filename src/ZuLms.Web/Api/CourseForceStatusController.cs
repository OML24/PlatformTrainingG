using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Features.Courses;
using ZuLms.Domain.Enums;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/courses/{id:long}/force-status")]
public sealed class CourseForceStatusController(ICourseForceStatusService service)
    : ControllerBase
{
    [HttpPost]
    [RequirePermission("course.force_status")]
    public async Task<IActionResult> Change(long id, ForceCourseStatusRequest input,
        CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await service.ChangeAsync(actor, id, input.ExpectedVersion,
            input.TargetStatus, input.Reason, input.Confirmed, ct);
        return result.Ok ? NoContent() : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }
}

public sealed record ForceCourseStatusRequest(uint ExpectedVersion,
    CourseStatus TargetStatus, string Reason, bool Confirmed);

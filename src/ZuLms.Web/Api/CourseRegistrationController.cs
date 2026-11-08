using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/courses")]
public sealed class CourseRegistrationController(ISelfRegistrationService registration) : ControllerBase
{
    [HttpGet("available")]
    [RequirePermission("participants.self_register")]
    public async Task<IActionResult> Available([FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await registration.ListAvailableAsync(actor, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost("{courseId:long}/self-registration")]
    [RequirePermission("participants.self_register")]
    public async Task<IActionResult> Register(long courseId, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await registration.RegisterAsync(actor, courseId, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    private bool TryActor(out long actor) => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "REG_FULL" or "REG_ALREADY" => Conflict(new { code }),
            _ => BadRequest(new { code })
        };
    }
}

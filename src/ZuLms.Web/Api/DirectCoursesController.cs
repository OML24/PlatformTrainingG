using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/courses/direct")]
public sealed class DirectCoursesController(IDirectCourseService courses) : ControllerBase
{
    [HttpGet("lookups")]
    [RequirePermission("course.create_direct")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await courses.LookupsAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Forbid();
    }

    [HttpGet("instructors")]
    [RequirePermission("course.create_direct")]
    public async Task<IActionResult> Instructors([FromQuery] bool external,
        [FromQuery] string? search, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await courses.SearchInstructorsAsync(actor, external, search, ct);
        return result.Ok ? Ok(result.Value) : Forbid();
    }

    [HttpPost]
    [RequirePermission("course.create_direct")]
    public async Task<IActionResult> Create(DirectCourseInput input, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await courses.CreateAsync(actor, input, ct);
        if (result.Ok) return Created($"/api/v1/courses/{result.Value}", new { id = result.Value });
        string code = result.Error?.Code ?? "UNKNOWN";
        return code == "FORBIDDEN" ? Forbid() : BadRequest(new { code });
    }

    [HttpGet("historical-employees")]
    [RequirePermission("course.create_historical")]
    public async Task<IActionResult> HistoricalEmployees([FromQuery] long centerId,
        CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await courses.HistoricalEmployeesAsync(actor, centerId, ct);
        return result.Ok ? Ok(result.Value) : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }
}

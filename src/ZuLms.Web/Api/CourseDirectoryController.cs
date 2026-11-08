using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Features.Courses;
using ZuLms.Domain.Enums;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/courses/directory")]
public sealed class CourseDirectoryController(ICourseDirectoryService directory)
    : ControllerBase
{
    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups(
        [FromQuery] CourseDirectoryView view = CourseDirectoryView.Auto,
        CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await directory.LookupsAsync(actor, view, ct);
        return result.Ok ? Ok(result.Value) : Forbid();
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] CourseDirectoryView view = CourseDirectoryView.Auto,
        [FromQuery] string? search = null,
        [FromQuery] CourseStatus? status = null,
        [FromQuery] long? centerId = null,
        [FromQuery] long? trackId = null,
        [FromQuery] DeliveryMode? deliveryMode = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isArchived = null,
        [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await directory.ListAsync(actor,
            new(view, search, status, centerId, trackId, deliveryMode,
                fromDate, toDate, isActive, isArchived, page), ct);
        return result.Ok ? Ok(result.Value) : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id,
        [FromQuery] CourseDirectoryView view = CourseDirectoryView.Auto,
        CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await directory.GetAsync(actor, id, view, ct);
        return result.Ok ? Ok(result.Value) : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : NotFound(new { code = result.Error?.Code });
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
}

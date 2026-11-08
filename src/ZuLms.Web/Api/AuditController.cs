using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Web.Security;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Audit;
using ZuLms.Application.Features.Notifications;
using ZuLms.Domain.Enums;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/audit")]
public sealed class AuditController(IAuditQueryService audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("page.admin.audit")]
    public async Task<IActionResult> List([FromQuery] string? search,
        [FromQuery] AuditCategory? category, [FromQuery] string? actionCode,
        [FromQuery] long? actorUserId, [FromQuery] long? courseId,
        [FromQuery] bool? success, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await audit.ListAsync(actor,
            new AuditFilter(search, category, actionCode, actorUserId, courseId,
                success, from, to, page), ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("live")]
    [RequirePermission("page.admin.live_feed")]
    public async Task<IActionResult> Live([FromQuery] long afterId = 0,
        CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await audit.LiveAsync(actor, afterId, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("export")]
    [RequirePermission("audit.export")]
    public async Task<IActionResult> Export([FromQuery] string? search,
        [FromQuery] AuditCategory? category, [FromQuery] string? actionCode,
        [FromQuery] long? actorUserId, [FromQuery] long? courseId,
        [FromQuery] bool? success, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await audit.ExportCsvAsync(actor,
            new AuditFilter(search, category, actionCode, actorUserId, courseId,
                success, from, to, 1), ct);
        if (!result.Ok) return Error(result);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(result.Value!)).ToArray(),
            "text/csv; charset=utf-8", "audit-log.csv");
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result) =>
        result.Error?.Code == "FORBIDDEN" ? Forbid() : BadRequest(new { code = result.Error?.Code });
}

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool unread = false,
        CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await notifications.ListAsync(actor, unread, ct);
        return result.Ok ? Ok(result.Value) : Forbid();
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await notifications.MarkReadAsync(actor, id, ct);
        return result.Ok ? NoContent() : BadRequest(new { code = result.Error?.Code });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await notifications.MarkAllReadAsync(actor, ct);
        return result.Ok ? Ok(new { marked = result.Value }) : Forbid();
    }

    [HttpPost("read-links")]
    public async Task<IActionResult> MarkLinksRead(MarkLinksReadRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await notifications.MarkLinksReadAsync(actor, request.Links ?? [], ct);
        return result.Ok ? Ok(new { marked = result.Value }) : Forbid();
    }

    public sealed record MarkLinksReadRequest(IReadOnlyList<string>? Links);

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
}

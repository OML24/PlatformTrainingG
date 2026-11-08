using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Needs;
using ZuLms.Domain.Enums;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/needs")]
public sealed class NeedsController(INeedService needs) : ControllerBase
{
    [HttpGet("lookups")]
    [RequirePermission("need.submit")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await needs.LookupsAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("instructors")]
    [RequirePermission("need.submit")]
    public async Task<IActionResult> Instructors([FromQuery] long facultyId,
        [FromQuery] string? search, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await needs.SearchInstructorsAsync(actor, facultyId, search, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet]
    [RequirePermission("need.review", "need.submit")]
    public async Task<IActionResult> List([FromQuery] string? search,
        [FromQuery] NeedStatus? status, [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await needs.ListAsync(actor, search, status, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("{id:long}")]
    [RequirePermission("need.review", "need.submit")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await needs.GetAsync(actor, id, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost]
    [RequirePermission("need.submit")]
    public async Task<IActionResult> Create(NeedInput input, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await needs.CreateAsync(actor, input, ct);
        return result.Ok ? Created($"/api/v1/needs/{result.Value}",
            new { id = result.Value }) : Error(result);
    }

    [HttpPost("{id:long}/resubmit")]
    [RequirePermission("need.resubmit")]
    public async Task<IActionResult> Resubmit(long id, NeedInput input,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await needs.ResubmitAsync(actor, id, input, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpPost("{id:long}/decision")]
    [RequirePermission("need.review")]
    public async Task<IActionResult> Decide(long id, NeedDecisionRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await needs.DecideAsync(actor, id, request.Approve, request.Notes, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpPost("{id:long}/deactivate")]
    public async Task<IActionResult> Deactivate(long id, DeactivateNeedRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await needs.DeactivateAsync(actor, id, request.Reason, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "NEED_NOT_FOUND" => NotFound(new { code }),
            _ => BadRequest(new { code })
        };
    }
}

public sealed record NeedDecisionRequest(bool Approve, string? Notes);
public sealed record DeactivateNeedRequest(string Reason);

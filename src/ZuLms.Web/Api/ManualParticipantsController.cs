using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/courses/{courseId:long}/participants")]
public sealed class ManualParticipantsController(IManualParticipantService participants)
    : ControllerBase
{
    [HttpGet]
    [RequirePermission("participants.add", "participants.remove")]
    public async Task<IActionResult> List(long courseId, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await participants.ListAsync(actor, courseId, ct);
        return result.Ok ? Ok(result.Value) : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : NotFound(new { code = result.Error?.Code });
    }

    [HttpPost]
    [RequirePermission("participants.add")]
    public async Task<IActionResult> Add(long courseId, AddParticipantsRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await participants.AddAsync(actor, courseId, request.UserIds,
            request.ConfirmCapacityOverride, ct);
        return Respond(result);
    }

    [HttpDelete("{userId:long}")]
    [RequirePermission("participants.remove")]
    public async Task<IActionResult> Remove(long courseId, long userId,
        RemoveParticipantRequest request, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await participants.RemoveAsync(actor, courseId, userId,
            request.Reason, request.ConfirmCertificateRevocation, ct);
        return Respond(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);

    private IActionResult Respond(Result result) => result.Ok ? NoContent() :
        result.Error?.Code == "FORBIDDEN" ? Forbid() :
        BadRequest(new { code = result.Error?.Code });
}

[ApiController]
[Route("api/v1/participants/employees")]
public sealed class ParticipantEmployeeSearchController(IManualParticipantService participants)
    : ControllerBase
{
    [HttpGet]
    [RequirePermission("participants.add")]
    public async Task<IActionResult> Search([FromQuery] string? search,
        CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await participants.SearchEmployeesAsync(actor, search, ct);
        return result.Ok ? Ok(result.Value) : Forbid();
    }
}

public sealed record AddParticipantsRequest(IReadOnlyList<long> UserIds,
    bool ConfirmCapacityOverride);
public sealed record RemoveParticipantRequest(string Reason,
    bool ConfirmCertificateRevocation);

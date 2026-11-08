using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/external-instructor-requests")]
public sealed class ExternalInstructorRequestsController(IExternalInstructorAccountService service)
    : ControllerBase
{
    [HttpGet]
    [RequirePermission("page.admin.users")]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await service.ListPendingAsync(actor, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("suggest-login")]
    [RequirePermission("user.create")]
    public async Task<IActionResult> SuggestLogin(CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await service.SuggestLoginAsync(actor, ct);
        return result.Ok ? Ok(new { loginName = result.Value }) : Error(result);
    }

    [HttpPost("{id:long}/complete")]
    [RequirePermission("user.create")]
    public async Task<IActionResult> Complete(long id, CompleteRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await service.CompleteAsync(actor, id, request.LoginName,
            request.Password, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    private bool TryActor(out long actor) => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "EXTERNAL_REQUEST_NOT_FOUND" => NotFound(new { code }),
            "EXTERNAL_REQUEST_NOT_PENDING" or "LOGIN_EXISTS" => Conflict(new { code }),
            _ => BadRequest(new { code })
        };
    }

    public sealed record CompleteRequest(string LoginName, string? Password);
}

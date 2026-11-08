using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Credentials;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/credentials")]
public sealed class CredentialsController(ICredentialService credentials) : ControllerBase
{
    [HttpPost("reauthenticate")]
    [RequirePermission("credentials.reveal")]
    public async Task<IActionResult> Reauthenticate(ReauthRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await credentials.ReauthenticateAsync(actor, request.Password, ct));
    }

    [HttpGet("{userId:long}/reveal")]
    [RequirePermission("credentials.reveal")]
    public async Task<IActionResult> Reveal(long userId, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        var result = await credentials.RevealAsync(actor, userId, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPut("{userId:long}/password")]
    [RequirePermission("credentials.change")]
    public async Task<IActionResult> Change(long userId, ChangeRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await credentials.ChangeAsync(actor, userId, request.ExpectedVersion,
            request.NewPassword, request.RequireChangeAtNextLogin, ct));
    }

    [HttpPut("shared-initial")]
    [RequirePermission("credentials.set_shared_initial")]
    public async Task<IActionResult> SetSharedInitial(SharedInitialRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await credentials.SetSharedInitialAsync(actor, request.ExpectedVersion,
            request.Password, ct));
    }

    [HttpPost("{userId:long}/send-initial")]
    [RequirePermission("credentials.send_initial")]
    public async Task<IActionResult> SendInitial(long userId, SendInitialRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await credentials.QueueInitialAsync(actor, userId, request.ConfirmResend, ct));
    }

    [HttpGet("send-initial/preview")]
    [RequirePermission("credentials.send_initial")]
    public async Task<IActionResult> PreviewBulk(CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await credentials.PreviewBulkInitialAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost("send-initial/bulk")]
    [RequirePermission("credentials.send_initial")]
    public async Task<IActionResult> SendBulk(CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await credentials.QueueBulkInitialAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost("{userId:long}/resolve-uncertain")]
    [RequirePermission("credentials.send_initial")]
    public async Task<IActionResult> ResolveUncertain(long userId, ResolveUncertainRequest request,
        CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        return Map(await credentials.ResolveUncertainAsync(actor, userId, request.Received, ct));
    }

    private bool TryActor(out long actor) => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Map(Result result) => result.Ok ? NoContent() : Error(result);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "USER_NOT_FOUND" => NotFound(new { code }),
            "CONFLICT" => Conflict(new { code }),
            "REAUTH_REQUIRED" => StatusCode(StatusCodes.Status401Unauthorized, new { code }),
            _ => BadRequest(new { code })
        };
    }

    public sealed record ReauthRequest(string Password);
    public sealed record ChangeRequest(uint ExpectedVersion, string NewPassword,
        bool RequireChangeAtNextLogin);
    public sealed record SharedInitialRequest(uint ExpectedVersion, string Password);
    public sealed record SendInitialRequest(bool ConfirmResend);
    public sealed record ResolveUncertainRequest(bool Received);
}

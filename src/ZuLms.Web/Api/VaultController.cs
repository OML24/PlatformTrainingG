using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Web.Security;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Files;
using ZuLms.Application.Features.Vault;
using ZuLms.Domain.Enums;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/vault")]
public sealed class VaultController(IVaultService vault) : ControllerBase
{
    [HttpGet]
    [RequirePermission("page.vault")]
    public async Task<IActionResult> List([FromQuery] string? search,
        [FromQuery] FileKind? docType, [FromQuery] long? courseId,
        [FromQuery] long? centerId, [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await vault.ListAsync(actor, search, docType, courseId,
            centerId, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost]
    [RequirePermission("vault.upload")]
    public async Task<IActionResult> Link(LinkVaultDocumentRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await vault.LinkAsync(actor, request.FileId, request.DocType,
            request.TitleAr, request.CourseId, request.CenterId, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpGet("{id:long}/file")]
    [RequirePermission("vault.download")]
    public async Task<IActionResult> File(long id, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await vault.GetFileAsync(actor, id, ct);
        if (!result.Ok) return Error(result);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value!.Content, result.Value.MimeType, result.Value.OriginalName);
    }

    [HttpPost("{id:long}/deactivate")]
    [RequirePermission("vault.deactivate")]
    public async Task<IActionResult> Deactivate(long id, DeactivateVaultRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await vault.DeactivateAsync(actor, id, request.Reason, ct);
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
            "VAULT_NOT_FOUND" or "FILE_NOT_FOUND" or "FILE_CONTENT_MISSING" =>
                NotFound(new { code }),
            _ => BadRequest(new { code })
        };
    }
}

public sealed record LinkVaultDocumentRequest(long FileId, FileKind DocType,
    string TitleAr, long? CourseId, long? CenterId);
public sealed record DeactivateVaultRequest(string Reason);

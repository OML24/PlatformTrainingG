using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Web.Security;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Certificates;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/certificate-template")]
public sealed class CertificateTemplateController(ICertificateTemplateService templates)
    : ControllerBase
{
    [HttpGet]
    [RequirePermission("cert_template.upload")]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await templates.GetCurrentAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost]
    [RequirePermission("cert_template.upload")]
    [RequestSizeLimit(52_500_000)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file,
        [FromForm] string? layoutJson, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest(new { code = "FILE_REQUIRED" });
        await using var stream = file.OpenReadStream();
        var result = await templates.UploadAsync(actor, file.FileName, stream,
            file.Length, layoutJson ?? string.Empty, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost("preview")]
    [RequirePermission("cert_template.upload")]
    public async Task<IActionResult> Preview(CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await templates.PreviewAsync(actor, ct);
        if (!result.Ok) return Error(result);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value!, "application/pdf", "certificate-preview.pdf");
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "CERT_TEMPLATE_MISSING" => NotFound(new { code }),
            _ => BadRequest(new { code })
        };
    }
}

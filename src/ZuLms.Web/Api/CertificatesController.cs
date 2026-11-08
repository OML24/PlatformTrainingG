using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Web.Security;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Certificates;
using ZuLms.Domain.Enums;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/certificates")]
public sealed class CertificatesController(ICertificateService certificates) : ControllerBase
{
    [HttpGet]
    [RequirePermission("certificates.view.all", "certificates.view.faculty")]
    public async Task<IActionResult> List([FromQuery] string? search,
        [FromQuery] long? courseId, [FromQuery] CertificateStatus? status,
        [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await certificates.ListAsync(actor, search, courseId, status, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("mine")]
    [RequirePermission("certificates.view.own")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await certificates.MineAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Forbid();
    }

    [HttpGet("{id:long}/pdf")]
    [RequirePermission("certificates.download.own")]
    public async Task<IActionResult> Pdf(long id, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await certificates.GetPdfAsync(actor, id, ct);
        if (!result.Ok) return Error(result);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value!.Content, result.Value.MimeType, result.Value.OriginalName);
    }

    [HttpPost("issue-pending")]
    [RequirePermission("certificates.issue_pending")]
    public async Task<IActionResult> IssuePending([FromQuery] long? courseId,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await certificates.IssuePendingAsync(actor, courseId, ct);
        return result.Ok ? Ok(new { issued = result.Value }) : Error(result);
    }

    [HttpPost("{id:long}/revoke")]
    [RequirePermission("certificates.revoke_manual")]
    public async Task<IActionResult> Revoke(long id, RevokeCertificateRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await certificates.RevokeAsync(actor, id, request.Reason, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpPost("force-issue")]
    [RequirePermission("certificates.issue_pending")]
    public async Task<IActionResult> ForceIssue(ForceIssueCertificateRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await certificates.ForceIssueAsync(actor, request.CourseId,
            request.EmployeeCode, request.EvaluationExempt, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "CERT_NOT_FOUND" or "FILE_CONTENT_MISSING" => NotFound(new { code }),
            _ => BadRequest(new { code })
        };
    }
}

public sealed record RevokeCertificateRequest(string Reason);
public sealed record ForceIssueCertificateRequest(long CourseId, int EmployeeCode,
    bool EvaluationExempt);

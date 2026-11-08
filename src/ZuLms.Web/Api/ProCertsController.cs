using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.ProCerts;
using ZuLms.Domain.Enums;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/procerts")]
public sealed class ProCertsController(IProCertService proCerts) : ControllerBase
{
    [HttpGet]
    [RequirePermission("procert.submit", "page.quality.pro_certs")]
    public async Task<IActionResult> List([FromQuery] string? search,
        [FromQuery] ProCertStatus? status, [FromQuery] long? categoryId,
        [FromQuery] bool includeInactive = false, [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await proCerts.ListAsync(actor, search, status, categoryId,
            includeInactive, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct)
    {
        var result = await proCerts.CategoriesAsync(ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost("categories")]
    [RequirePermission("procert.categories.manage")]
    public async Task<IActionResult> SaveCategory(ProCertCategoryRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await proCerts.SaveCategoryAsync(actor, request.Id,
            request.NameAr, request.NameEn, request.SortOrder, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpGet("{id:long}")]
    [RequirePermission("procert.submit", "page.quality.pro_certs")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await proCerts.GetAsync(actor, id, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost]
    [RequirePermission("procert.submit")]
    public async Task<IActionResult> Submit(ProCertInput input, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await proCerts.SubmitAsync(actor, input, ct);
        return result.Ok ? Created($"/api/v1/procerts/{result.Value}",
            new { id = result.Value }) : Error(result);
    }

    [HttpPut("{id:long}")]
    [RequirePermission("procert.edit")]
    public async Task<IActionResult> Edit(long id, ProCertInput input, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await proCerts.EditAsync(actor, id, input, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpPost("{id:long}/resubmit")]
    [RequirePermission("procert.submit")]
    public async Task<IActionResult> Resubmit(long id, ProCertInput input,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await proCerts.ResubmitAsync(actor, id, input, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpPost("{id:long}/withdraw")]
    [RequirePermission("procert.withdraw")]
    public async Task<IActionResult> Withdraw(long id, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await proCerts.WithdrawAsync(actor, id, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpPost("{id:long}/decision")]
    [RequirePermission("procert.review")]
    public async Task<IActionResult> Decide(long id, ProCertDecisionRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await proCerts.DecideAsync(actor, id, request.Approve,
            request.Notes, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpPost("{id:long}/deactivate")]
    [RequirePermission("procert.deactivate")]
    public async Task<IActionResult> Deactivate(long id, DeactivateProCertRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await proCerts.DeactivateAsync(actor, id, request.Reason, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpGet("{id:long}/file")]
    [RequirePermission("procert.download.own", "procert.download.any")]
    public async Task<IActionResult> File(long id, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await proCerts.GetFileAsync(actor, id, ct);
        if (!result.Ok) return Error(result);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value!.Content, result.Value.MimeType,
            result.Value.OriginalName);
    }

    [HttpGet("stats")]
    [RequirePermission("procert.stats")]
    public async Task<IActionResult> Stats([FromQuery] long? facultyId,
        [FromQuery] long? categoryId, [FromQuery] string? issuer,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] bool includeExpired = true, CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await proCerts.StatsAsync(actor, facultyId, categoryId, issuer,
            from, to, includeExpired, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("stats/export")]
    [RequirePermission("procert.stats")]
    public async Task<IActionResult> Export([FromQuery] long? facultyId,
        [FromQuery] long? categoryId, [FromQuery] string? issuer,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] bool includeExpired = true, CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await proCerts.StatsAsync(actor, facultyId, categoryId, issuer,
            from, to, includeExpired, ct);
        if (!result.Ok) return Error(result);
        var stats = result.Value!;
        var builder = new StringBuilder();
        builder.AppendLine("section,name,value");
        builder.AppendLine($"totals,certs,{stats.Totals.Certs}");
        builder.AppendLine($"totals,members,{stats.Totals.Members}");
        builder.AppendLine($"totals,avgPerMember,{stats.Totals.AvgPerMember}");
        builder.AppendLine($"totals,coveragePercent,{stats.Totals.CoveragePercent}");
        foreach (var member in stats.TopMembers)
            builder.AppendLine($"topMembers,{Escape(member.Name)},{member.Count}");
        foreach (var faculty in stats.ByFaculty)
            builder.AppendLine($"byFaculty,{Escape(faculty.FacultyName)},{faculty.Certs}");
        foreach (var category in stats.ByCategory)
            builder.AppendLine($"byCategory,{Escape(category.Name)},{category.Certs}");
        foreach (var issuerRow in stats.TopIssuers)
            builder.AppendLine($"topIssuers,{Escape(issuerRow.Issuer)},{issuerRow.Certs}");
        foreach (var year in stats.ByYear)
            builder.AppendLine($"byYear,{year.Year},{year.Certs}");
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray(), "text/csv; charset=utf-8",
            "procert-stats.csv");
    }

    private static string Escape(string value)
    {
        string safe = value.TrimStart(' ', '\t', '\r', '\n');
        if (safe.Length > 0 && "=+-@".Contains(safe[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "PROCERT_NOT_FOUND" or "CATEGORY_NOT_FOUND" => NotFound(new { code }),
            _ => BadRequest(new { code })
        };
    }
}

public sealed record ProCertDecisionRequest(bool Approve, string? Notes);
public sealed record DeactivateProCertRequest(string Reason);
public sealed record ProCertCategoryRequest(long? Id, string NameAr, string? NameEn,
    int SortOrder);

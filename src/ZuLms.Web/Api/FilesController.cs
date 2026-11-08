using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Files;
using ZuLms.Domain.Enums;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/files")]
public sealed class FilesController(IFileUploadService files) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(525_000_000)]
    public async Task<IActionResult> Upload([FromForm] FileKind? kind,
        [FromForm] IFormFile file, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        if (file is null) return BadRequest(new { code = "FILE_REQUIRED" });
        if (kind is null || !Enum.IsDefined(kind.Value))
            return BadRequest(new { code = "FILE_KIND_INVALID" });
        await using var stream = file.OpenReadStream();
        var result = await files.UploadAsync(actor, kind.Value, file.FileName,
            stream, file.Length, ct);
        return result.Ok ? Created($"/api/v1/files/{result.Value!.Id}", result.Value) : Error(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Download(long id, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await files.DownloadAsync(actor, id, ct);
        if (!result.Ok)
        {
            if (result.Error?.Code == "FILE_CONTENT_MISSING" &&
                Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return Content("<!doctype html><html lang=\"ar\" dir=\"rtl\"><meta charset=\"utf-8\"><title>الملف غير متاح</title><main style=\"max-width:40rem;margin:5rem auto;font-family:sans-serif\"><h1>الملف غير متاح</h1><p>تعذر العثور على محتوى هذا الملف. يرجى التواصل مع الإدارة.</p><a href=\"/\">العودة</a></main></html>", "text/html; charset=utf-8");
            }
            return Error(result);
        }
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Value!.Content, result.Value.MimeType,
            result.Value.OriginalName);
    }

    private bool TryActor(out long actor) => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "FILE_NOT_FOUND" or "FILE_CONTENT_MISSING" => NotFound(new { code }),
            _ => BadRequest(new { code })
        };
    }
}

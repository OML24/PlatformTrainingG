using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/evaluation-form/draft")]
public sealed class EvaluationFormAdminController(
    IEvaluationFormAdminService forms) : ControllerBase
{
    [HttpGet]
    [RequirePermission("evalform.edit")]
    public async Task<IActionResult> Open(CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await forms.OpenDraftAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPut("{draftId:long}")]
    [RequirePermission("evalform.edit")]
    public async Task<IActionResult> Save(long draftId,
        EvaluationDraftInput input, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await forms.SaveDraftAsync(actor, draftId, input, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpPost("{draftId:long}/publish")]
    [RequirePermission("evalform.edit")]
    public async Task<IActionResult> Publish(long draftId, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await forms.PublishAsync(actor, draftId, ct);
        return result.Ok ? Ok(new { versionNo = result.Value }) : Error(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result) => result.Error?.Code == "FORBIDDEN"
        ? Forbid() : BadRequest(new { code = result.Error?.Code });
}

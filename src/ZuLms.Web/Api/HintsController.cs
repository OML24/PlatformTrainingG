using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Hints;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/hints")]
public sealed class HintsController(IHintService hints) : ControllerBase
{
    /// <summary>
    /// Hints for a page context (01 §6.4), or — for administrators holding <c>hints.manage</c> —
    /// the paged catalogue used by <c>/admin/hints</c>.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? context, [FromQuery] string? q,
        [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(context))
        {
            var byContext = await hints.ListByContextAsync(context, ct);
            return byContext.Ok ? Ok(byContext.Value) : Error(byContext);
        }
        if (!TryActor(out long actor)) return Unauthorized();
        var catalogue = await hints.ListAllAsync(actor, q, page, ct);
        return catalogue.Ok ? Ok(catalogue.Value) : Error(catalogue);
    }

    [HttpPost]
    [RequirePermission("hints.manage")]
    public async Task<IActionResult> Create(SaveHintInput request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await hints.CreateAsync(actor, request, ct);
        return result.Ok ? Created($"/api/v1/hints/{result.Value}", new { id = result.Value }) : Error(result);
    }

    [HttpPut("{id:long}")]
    [RequirePermission("hints.manage")]
    public async Task<IActionResult> Update(long id, SaveHintInput request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await hints.UpdateAsync(actor, id, request, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpDelete("{id:long}")]
    [RequirePermission("hints.manage")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await hints.DeactivateAsync(actor, id, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    private bool TryActor(out long actor) =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);

    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" => Forbid(),
            "HINT_NOT_FOUND" => NotFound(new { code }),
            "HINT_EXISTS" => Conflict(new { code }),
            _ => BadRequest(new { code })
        };
    }
}

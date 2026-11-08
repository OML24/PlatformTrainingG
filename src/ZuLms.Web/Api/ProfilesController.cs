using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Profiles;
using ZuLms.Application.Features.Tracks;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/profiles")]
public sealed class ProfilesController(IProfileService profiles) : ControllerBase
{
    [HttpGet("directory")]
    [RequirePermission("page.shared.directory")]
    public async Task<IActionResult> Directory([FromQuery] string? search,
        [FromQuery] long? centerId, [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await profiles.DirectoryAsync(actor, search, centerId, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("{userId:long}")]
    [RequirePermission("page.shared.profile")]
    public async Task<IActionResult> Get(long userId, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await profiles.GetAsync(actor, userId, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPut("me")]
    [RequirePermission("profile.edit.own")]
    public async Task<IActionResult> UpdateOwn(UpdateProfileRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await profiles.UpdateOwnAsync(actor, request.BioAr, request.BioEn,
            request.ContactMethod, request.ContactValue, request.ShowInDirectory, ct);
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
            "USER_NOT_FOUND" => NotFound(new { code }),
            _ => BadRequest(new { code })
        };
    }
}

public sealed record UpdateProfileRequest(string? BioAr, string? BioEn,
    string? ContactMethod, string? ContactValue, bool ShowInDirectory);

[ApiController]
[Authorize]
[Route("api/v1/tracks")]
public sealed class TracksController(ITrackAdminService tracks) : ControllerBase
{
    [HttpGet]
    [RequirePermission("tracks.manage")]
    public async Task<IActionResult> List([FromQuery] bool includeArchived = false,
        CancellationToken ct = default)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await tracks.ListAsync(actor, includeArchived, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost]
    [RequirePermission("tracks.manage")]
    public async Task<IActionResult> Create(SaveTrackRequest request, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await tracks.SaveAsync(actor, null, request.NameAr,
            request.NameEn, request.SortOrder, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpPut("{id:long}")]
    [RequirePermission("tracks.manage")]
    public async Task<IActionResult> Update(long id, SaveTrackRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await tracks.SaveAsync(actor, id, request.NameAr,
            request.NameEn, request.SortOrder, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpPost("{id:long}/archive")]
    [RequirePermission("tracks.manage")]
    public async Task<IActionResult> Archive(long id, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await tracks.SetArchivedAsync(actor, id, true, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpPost("{id:long}/unarchive")]
    [RequirePermission("tracks.manage")]
    public async Task<IActionResult> Unarchive(long id, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await tracks.SetArchivedAsync(actor, id, false, ct);
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
            "TRACK_NOT_FOUND" => NotFound(new { code }),
            _ => BadRequest(new { code })
        };
    }
}

public sealed record SaveTrackRequest(string NameAr, string? NameEn, int SortOrder);

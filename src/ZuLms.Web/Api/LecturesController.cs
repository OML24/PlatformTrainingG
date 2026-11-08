using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Authorize]
[Route("api/v1/courses/{courseId:long}/lectures")]
public sealed class CourseLecturesController(ILectureService lectures) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(long courseId, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await lectures.ListAsync(actor, courseId, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(long courseId, LectureInput input,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await lectures.CreateAsync(actor, courseId, input, ct);
        return result.Ok ? Created($"/api/v1/courses/{courseId}/lectures/{result.Value}",
            new { id = result.Value }) : result.Error?.Code == "LECTURE_INVALID"
                ? BadRequest(new { code = "LECTURE_INVALID", fieldErrors = LectureFields(input) })
                : Error(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private static Dictionary<string, string[]> LectureFields(LectureInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.StartsAt == default || input.StartsAt.Kind != DateTimeKind.Unspecified)
            errors["startsAt"] = ["وقت البداية غير صالح"];
        if (input.EndsAt <= input.StartsAt || input.EndsAt.Kind != DateTimeKind.Unspecified)
            errors["endsAt"] = ["وقت النهاية يجب أن يأتي بعد البداية"];
        if (input.TitleAr?.Length > 300 || input.TitleEn?.Length > 300)
            errors["titleAr"] = ["عنوان المحاضرة طويل جداً"];
        if (input.LocationAr?.Length > 300 || input.LocationEn?.Length > 300)
            errors["locationAr"] = ["المكان طويل جداً"];
        if (input.TeamsUrl?.Length > 600)
            errors["teamsUrl"] = ["الرابط طويل جداً"];
        if (errors.Count == 0) errors["startsAt"] = ["تحقق من التوقيت والنمط"];
        return errors;
    }
    private IActionResult Error(Result result) => result.Error?.Code switch
    {
        "FORBIDDEN" => Forbid(),
        "LECTURE_NOT_FOUND" => NotFound(new { code = "LECTURE_NOT_FOUND" }),
        _ => BadRequest(new { code = result.Error?.Code })
    };
}

[ApiController]
[Route("api/v1/lectures")]
public sealed class LectureCodeController(ILectureService lectures) : ControllerBase
{
    [HttpPut("{id:long}")]
    [RequirePermission("lecture.manage")]
    public async Task<IActionResult> Update(long id, LectureScheduleInput input,
        CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await lectures.UpdateAsync(actor, id, input, ct);
        return result.Ok ? NoContent() : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }

    [HttpPost("{id:long}/activate-code")]
    [RequirePermission("attendance.activate_code")]
    public async Task<IActionResult> Activate(long id, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await lectures.ActivateCodeAsync(actor, id, ct);
        return result.Ok ? Ok(result.Value) : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }
}

[ApiController]
[Route("api/v1/lectures/{id:long}/attendance")]
public sealed class AttendanceCodeController(IAttendanceCodeService attendance)
    : ControllerBase
{
    [HttpPost("enter-code")]
    [RequirePermission("attendance.enter_code")]
    public async Task<IActionResult> Enter(long id, EnterCodeRequest request,
        CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long actor))
            return Unauthorized();
        var result = await attendance.EnterAsync(actor, id, request.Code, ct);
        return result.Ok ? NoContent() : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }
}

public sealed record EnterCodeRequest(string Code);

[ApiController]
[Route("api/v1/lectures/{id:long}/attendance/roster")]
public sealed class ManualAttendanceController(IManualAttendanceService attendance)
    : ControllerBase
{
    [HttpGet]
    [RequirePermission("attendance.edit_manual")]
    public async Task<IActionResult> Roster(long id, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await attendance.RosterAsync(actor, id, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPut("{participantId:long}")]
    [RequirePermission("attendance.edit_manual")]
    public async Task<IActionResult> Set(long id, long participantId,
        ManualAttendanceRequest request, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await attendance.SetAsync(actor, id, participantId,
            request.IsPresent, request.Reason, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result) => result.Error?.Code switch
    {
        "FORBIDDEN" => Forbid(),
        "LECTURE_NOT_FOUND" => NotFound(new { code = "LECTURE_NOT_FOUND" }),
        _ => BadRequest(new { code = result.Error?.Code })
    };
}

public sealed record ManualAttendanceRequest(bool IsPresent, string? Reason);

[ApiController]
[Route("api/v1/lectures/{id:long}")]
public sealed class LectureDeactivationController(ILectureDeactivationService lectures)
    : ControllerBase
{
    [HttpPost("deactivate")]
    [RequirePermission("lecture.manage")]
    public async Task<IActionResult> Deactivate(long id,
        DeactivateLectureRequest request, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),
            out long actor)) return Unauthorized();
        var result = await lectures.DeactivateAsync(actor, id,
            request.Reason, ct);
        return result.Ok ? NoContent() : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }
}

public sealed record DeactivateLectureRequest(string? Reason);

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Domain.Enums;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/courses/{courseId:long}/exam")]
public sealed class ExamsController(IExamService exams) : ControllerBase
{
    [HttpGet]
    [RequirePermission("exam.manage")]
    public async Task<IActionResult> Get(long courseId, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await exams.GetAsync(actor, courseId, ct);
        return result.Ok ? Ok(new { exam = result.Value }) : Error(result);
    }

    [HttpPost]
    [RequirePermission("exam.manage")]
    public async Task<IActionResult> Create(long courseId, CreateExamRequest request,
        CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await exams.CreateAsync(actor, courseId, request.NameAr, ct);
        return result.Ok ? Ok(new { id = result.Value }) : Error(result);
    }

    [HttpPut("participants/{participantId:long}")]
    [RequirePermission("exam.manage")]
    public async Task<IActionResult> Record(long courseId, long participantId,
        RecordExamResultRequest request, CancellationToken ct)
    {
        if (!Actor(out long actor)) return Unauthorized();
        var result = await exams.RecordAsync(actor, courseId, participantId,
            request.Outcome, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    private bool Actor(out long actor) => long.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result) => result.Error?.Code == "FORBIDDEN"
        ? Forbid() : BadRequest(new { code = result.Error?.Code });
}

public sealed record CreateExamRequest(string NameAr);
public sealed record RecordExamResultRequest(ExamOutcome Outcome);

[ApiController]
[Route("api/v1/courses/{courseId:long}/exam-requirement")]
public sealed class ExamRequirementController(IExamRequirementService requirements)
    : ControllerBase
{
    [HttpPut]
    [RequirePermission("exam.requirement.edit")]
    public async Task<IActionResult> Change(long courseId,
        ChangeExamRequirementRequest request, CancellationToken ct)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),
            out long actor)) return Unauthorized();
        var result = await requirements.ChangeAsync(actor, courseId,
            request.HasExam, request.DisableReason, ct);
        return result.Ok ? NoContent() : result.Error?.Code == "FORBIDDEN"
            ? Forbid() : BadRequest(new { code = result.Error?.Code });
    }
}

public sealed record ChangeExamRequirementRequest(bool HasExam,
    string? DisableReason);

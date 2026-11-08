using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Courses;
using ZuLms.Web.Security;

namespace ZuLms.Web.Api;

[ApiController]
[Route("api/v1/proposals")]
public sealed class CourseProposalsController(ICourseProposalService proposals,
    ICourseProposalQueryService queries) : ControllerBase
{
    [HttpGet("mine")]
    [RequirePermission("page.dean.courses")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await queries.ListMineAsync(actor, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("pending")]
    [RequirePermission("page.quality.proposals")]
    public async Task<IActionResult> Pending([FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await queries.ListPendingAsync(actor, page, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("dean-lookups")]
    [RequirePermission("page.dean.propose")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await queries.DeanLookupsAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("faculty-members")]
    [RequirePermission("page.dean.propose", "faculty.members.view")]
    public async Task<IActionResult> FacultyMembers([FromQuery] long centerId,
        [FromQuery] string? search, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await queries.SearchFacultyMembersAsync(actor, centerId, search, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("external-instructors")]
    [RequirePermission("page.quality.proposals", "page.quality.external_instructors")]
    public async Task<IActionResult> ExternalInstructors([FromQuery] string? search,
        CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await queries.SearchExternalInstructorsAsync(actor, search, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("faculties")]
    [RequirePermission("page.quality.proposals")]
    public async Task<IActionResult> Faculties(CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await queries.ListFacultiesAsync(actor, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpGet("{id:long}")]
    [RequirePermission("page.dean.courses", "page.quality.proposals")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await queries.GetAsync(actor, id, ct);
        return result.Ok ? Ok(result.Value) : Error(result);
    }

    [HttpPost]
    [RequirePermission("course.propose")]
    public async Task<IActionResult> Propose(ProposeRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await proposals.ProposeAsync(actor, request.CenterId, request.Input, ct);
        return result.Ok ? Created($"/api/v1/proposals/{result.Value}", new { id = result.Value }) : Error(result);
    }

    [HttpPost("{id:long}/resubmit")]
    [RequirePermission("proposal.resubmit")]
    public async Task<IActionResult> Resubmit(long id, ResubmitRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await proposals.ResubmitAsync(actor, id, request.ExpectedVersion,
            request.Input, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    [HttpPost("{id:long}/decision")]
    [RequirePermission("proposal.decide")]
    public async Task<IActionResult> Decide(long id, DecideRequest request, CancellationToken ct)
    {
        if (!TryActor(out long actor)) return Unauthorized();
        var result = await proposals.DecideAsync(actor, id, request.ExpectedVersion,
            request.Input, ct);
        return result.Ok ? NoContent() : Error(result);
    }

    private bool TryActor(out long actor) => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor);
    private IActionResult Error(Result result)
    {
        string code = result.Error?.Code ?? "UNKNOWN";
        return code switch
        {
            "FORBIDDEN" or "DEAN_SCOPE_REQUIRED" => Forbid(),
            "COURSE_NOT_FOUND" => NotFound(new { code }),
            "CONFLICT" or "PROPOSAL_NOT_PENDING" => Conflict(new { code }),
            "PROPOSAL_TITLE_INVALID" => BadRequest(new { code, fieldErrors = new { titleAr = new[] { "اسم الدورة غير صالح" } } }),
            "PROPOSAL_OBJECTIVES_INVALID" => BadRequest(new { code, fieldErrors = new { objectivesAr = new[] { "الأهداف مطلوبة" } } }),
            "PROPOSAL_AUDIENCE_INVALID" => BadRequest(new { code, fieldErrors = new { targetAudienceAr = new[] { "الفئة المستهدفة مطلوبة" } } }),
            "PROPOSAL_HOURS_INVALID" => BadRequest(new { code, fieldErrors = new { totalHours = new[] { "عدد الساعات غير صالح" } } }),
            "PROPOSAL_DATES_INVALID" => BadRequest(new { code, fieldErrors = new { plannedEndDate = new[] { "تاريخ النهاية يجب أن يأتي بعد البداية" } } }),
            _ => BadRequest(new { code })
        };
    }

    public sealed record ProposeRequest(long CenterId, CourseProposalInput Input);
    public sealed record ResubmitRequest(uint ExpectedVersion, CourseProposalInput Input);
    public sealed record DecideRequest(uint ExpectedVersion, CourseProposalDecisionInput Input);
}

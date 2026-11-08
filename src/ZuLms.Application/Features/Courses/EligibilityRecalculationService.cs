using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Courses;

// Bridges eligibility changes to certificate issuance/revocation inside the caller's transaction.
public sealed class EligibilityRecalculationService(IAppDbContext db, IClock clock,
    IEligibilitySnapshotService snapshots, IAuditLogger audit,
    ZuLms.Application.Features.Certificates.ICertificateService certificates)
    : IEligibilityRecalculationService
{
    public async Task<Result<EligibilityRecalculationDto>> RecalculateParticipantAsync(
        long courseId, long participantId, CertificateRevokeReason? revokeReason,
        long? actorId, CancellationToken ct)
    {
        var state = await snapshots.EvaluateAsync(courseId, participantId,
            false, ct);
        if (!state.Ok)
            return Result<EligibilityRecalculationDto>.Failure(state.Error!.Code);
        var participant = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.Id == participantId && x.CourseId == courseId)
            .Select(x => new { x.UserId }).SingleOrDefaultAsync(ct);
        var course = await db.Courses.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == courseId)
            .Select(x => new { x.TitleAr, x.TitleEn }).SingleOrDefaultAsync(ct);
        if (participant is null || course is null)
            return Result<EligibilityRecalculationDto>.Failure("PARTICIPANT_NOT_FOUND");
        var issued = await db.Certificates.IgnoreQueryFilters()
            .Where(x => x.CourseId == courseId &&
                x.ParticipantId == participantId &&
                x.Status == CertificateStatus.Issued).ToListAsync(ct);
        if (state.Value!.Eligible)
        {
            if (issued.Count > 0)
                return Result<EligibilityRecalculationDto>.Success(
                    new(true, false, null, 0));
            bool templateExists = await db.CertificateTemplates.AsNoTracking()
                .AnyAsync(x => x.IsCurrent, ct);
            if (!templateExists)
            {
                await audit.LogAsync(new AuditEntry(AuditCategory.Certificate,
                    "certificates.issue_pending", "إصدار شهادة معلّق",
                    "Certificate issuance pending", "CourseParticipant",
                    participantId, course.TitleAr, CourseId: courseId,
                    Success: false, FailureReason: "CERT_TEMPLATE_MISSING",
                    ActorUserId: actorId), ct);
                return Result<EligibilityRecalculationDto>.Success(
                    new(true, true, "CERT_TEMPLATE_MISSING", 0));
            }
            var issue = await certificates.IssueAsync(courseId, participantId, actorId, ct);
            if (issue.Ok)
                return Result<EligibilityRecalculationDto>.Success(
                    new(true, true, null, 1));
            // Renderer/template failures never break the business operation; issuance stays pending.
            await audit.LogAsync(new AuditEntry(AuditCategory.Certificate,
                "certificates.issue_pending", "إصدار شهادة معلّق",
                "Certificate issuance pending", "CourseParticipant",
                participantId, course.TitleAr, CourseId: courseId,
                Success: false, FailureReason: issue.Error!.Code,
                ActorUserId: actorId), ct);
            return Result<EligibilityRecalculationDto>.Success(
                new(true, true, issue.Error.Code, 0));
        }
        if (issued.Count == 0 || revokeReason is null)
            return Result<EligibilityRecalculationDto>.Success(
                new(false, false, null, 0));
        DateTimeOffset now = clock.UtcNow;
        foreach (var certificate in issued)
        {
            certificate.Status = CertificateStatus.Revoked;
            certificate.RevokedReason = revokeReason;
            certificate.RevokedAt = now;
            certificate.RevokedBy = actorId;
            certificate.RevokedNote = revokeReason.ToString();
            db.Notifications.Add(new Notification
            {
                UserId = participant.UserId,
                Type = NotificationType.CertificateRevoked,
                TitleAr = "إلغاء شهادة بعد تغيّر الاستحقاق",
                TitleEn = "Certificate revoked after eligibility change",
                BodyAr = $"{course.TitleAr} — {revokeReason}",
                BodyEn = $"{course.TitleEn ?? course.TitleAr} — {revokeReason}",
                LinkUrl = $"/courses/{courseId}",
                RelatedEntityType = "Certificate",
                RelatedEntityId = certificate.Id,
                CreatedAt = now, IsActive = true
            });
        }
        await audit.LogAsync(new AuditEntry(AuditCategory.Certificate,
            "certificate.revoke", "إلغاء شهادة لفقد الاستحقاق",
            "Certificate revoked after eligibility change", "CourseParticipant",
            participantId, course.TitleAr, CourseId: courseId,
            AfterJson: $"{{\"reason\":\"{revokeReason}\",\"count\":{issued.Count}}}",
            ActorUserId: actorId), ct);
        return Result<EligibilityRecalculationDto>.Success(
            new(false, false, null, issued.Count));
    }

    public async Task<Result> RecalculateCourseAsync(long courseId,
        CertificateRevokeReason? revokeReason, long? actorId,
        CancellationToken ct)
    {
        var participants = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.CourseId == courseId && x.IsActive)
            .Select(x => x.Id).ToListAsync(ct);
        foreach (long participantId in participants)
        {
            var result = await RecalculateParticipantAsync(courseId,
                participantId, revokeReason, actorId, ct);
            if (!result.Ok) return Result.Failure(result.Error!.Code);
        }
        return Result.Success();
    }
}

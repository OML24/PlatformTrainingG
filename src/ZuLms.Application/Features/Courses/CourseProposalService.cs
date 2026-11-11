using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public sealed class CourseProposalService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings, IAuditLogger audit)
    : ICourseProposalService
{
    public async Task<Result<long>> ProposeAsync(long deanUserId, long centerId,
        CourseProposalInput input, CancellationToken ct)
    {
        if (!await permissions.HasAsync(deanUserId, "course.propose", ct))
            return Result<long>.Failure("FORBIDDEN");
        if (!await IsDeanOfAsync(deanUserId, centerId, ct))
            return Result<long>.Failure("DEAN_SCOPE_REQUIRED");
        string? error = await ValidateAsync(input, centerId, deanUserId, ct);
        if (error is not null) return Result<long>.Failure(error);
        return await db.InSerializableTransactionAsync(async () =>
        {
            DateTimeOffset now = clock.UtcNow;
            int year = TimeZoneInfo.ConvertTime(now,
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman")).Year;
            string courseNo = await db.NextCourseNumberAsync(year, ct);
            var course = new Course
            {
                CourseNo = courseNo,
                Origin = CourseOrigin.DeanProposal,
                ProposingCenterId = centerId,
                ProposedByUserId = deanUserId,
                TargetCenterId = input.AudienceType == AudienceType.Faculty ? centerId : null,
                Status = CourseStatus.Proposed,
                StatusChangedAt = now,
                CurrentRevisionNo = 1,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = deanUserId
            };
            await ApplyInputAsync(course, input, ct);
            db.Courses.Add(course);
            await db.SaveChangesAsync(ct);
            await AddSubmissionAsync(course, deanUserId, input, ct);
            await LinkFilesAsync(course, input, deanUserId, ct);
            await NotifyQualityAsync(course, ct);
            await audit.LogAsync(new AuditEntry(AuditCategory.Course, "course.propose",
                "إرسال مقترح دورة", "Course proposal submitted", "Course",
                course.Id, course.TitleAr, ActorUserId: deanUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result<long>.Success(course.Id);
        }, ct);
    }

    public async Task<Result> ResubmitAsync(long deanUserId, long courseId,
        uint expectedVersion, CourseProposalInput input, CancellationToken ct)
    {
        if (!await permissions.HasAsync(deanUserId, "course.propose", ct))
            return Result.Failure("FORBIDDEN");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (course.Version != expectedVersion) return Result.Failure("CONFLICT");
            if (course.Origin != CourseOrigin.DeanProposal || course.Status != CourseStatus.Returned ||
                course.ProposedByUserId != deanUserId || course.ProposingCenterId is not long centerId ||
                !await IsDeanOfAsync(deanUserId, centerId, ct))
                return Result.Failure("PROPOSAL_RESUBMIT_NOT_ALLOWED");
            string? error = await ValidateAsync(input, centerId, deanUserId, ct);
            if (error is not null) return Result.Failure(error);
            DateTimeOffset now = clock.UtcNow;
            await ApplyInputAsync(course, input, ct);
            course.CurrentRevisionNo++;
            course.LastReturnNotes = null;
            course.Status = CourseStatus.Proposed;
            course.StatusChangedAt = now;
            course.UpdatedAt = now;
            course.UpdatedByUserId = deanUserId;
            await AddSubmissionAsync(course, deanUserId, input, ct);
            await LinkFilesAsync(course, input, deanUserId, ct);
            await NotifyQualityAsync(course, ct);
            await audit.LogAsync(new AuditEntry(AuditCategory.Course, "course.proposal_resubmit",
                "إعادة إرسال مقترح دورة", "Course proposal resubmitted", "Course",
                course.Id, course.TitleAr, ActorUserId: deanUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> DecideAsync(long actorUserId, long courseId,
        uint expectedVersion, CourseProposalDecisionInput input, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "proposal.decide", ct))
            return Result.Failure("FORBIDDEN");
        if (input.Decision is not (ProposalDecision.Approved or ProposalDecision.Returned or ProposalDecision.Rejected))
            return Result.Failure("PROPOSAL_DECISION_INVALID");
        if (input.Decision != ProposalDecision.Approved && string.IsNullOrWhiteSpace(input.Notes))
            return Result.Failure("REASON_REQUIRED");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == courseId, ct);
            if (course is null) return Result.Failure("COURSE_NOT_FOUND");
            if (course.Version != expectedVersion) return Result.Failure("CONFLICT");
            if (course.Origin != CourseOrigin.DeanProposal || course.Status != CourseStatus.Proposed)
                return Result.Failure("PROPOSAL_NOT_PENDING");
            var revision = await db.CourseProposalRevisions.SingleAsync(x =>
                x.CourseId == courseId && x.RevisionNo == course.CurrentRevisionNo, ct);
            if (revision.Decision != ProposalDecision.Pending)
                return Result.Failure("PROPOSAL_NOT_PENDING");
            if (input.Decision == ProposalDecision.Approved)
            {
                string? error = await ValidateApprovalAsync(course, revision, input, actorUserId, ct);
                if (error is not null) return Result.Failure(error);
            }
            CourseStatus next = input.Decision switch
            {
                ProposalDecision.Approved => ExternalInstructorCourseStatus.AfterApproval(input.ExternalDecision),
                ProposalDecision.Returned => CourseStatus.Returned,
                _ => CourseStatus.Rejected
            };
            if (!CourseStateMachine.CanTransition(course.Status, next))
                return Result.Failure("COURSE_TRANSITION_INVALID");
            DateTimeOffset now = clock.UtcNow;
            revision.Decision = input.Decision;
            revision.DecidedBy = actorUserId;
            revision.DecidedAt = now;
            revision.DecisionNotes = input.Notes?.Trim();
            course.Status = next;
            course.StatusChangedAt = now;
            course.UpdatedAt = now;
            course.UpdatedByUserId = actorUserId;
            course.LastReturnNotes = next == CourseStatus.Returned ? input.Notes!.Trim() : null;
            if (next is CourseStatus.Approved or CourseStatus.AwaitingAccount)
            {
                course.ApprovedBy = actorUserId;
                course.ApprovedAt = now;
                course.EvaluationEndDate = input.EvaluationEndDate;
                course.AudienceType = input.AudienceType!.Value;
                course.TargetCenterId = input.AudienceType == AudienceType.Faculty ? input.TargetCenterId : null;
                course.RegistrationDeadline = input.RegistrationDeadline;
                course.MaxParticipants = input.MaxParticipants;
                course.HasExam = input.HasExam!.Value;
                ApplyExternalDecision(course, revision, input, actorUserId);
            }
            db.CourseStatusHistorys.Add(new CourseStatusHistory
            {
                CourseId = courseId,
                FromStatus = CourseStatus.Proposed,
                ToStatus = next,
                ChangedBy = actorUserId,
                ChangedAt = now,
                Reason = input.Notes?.Trim()
            });
            await NotifyDecisionAsync(course, next, ct);
            await audit.LogAsync(new AuditEntry(AuditCategory.Course, "proposal.decide",
                "قرار الجودة على مقترح", "Proposal decision", "Course", courseId,
                course.TitleAr, BeforeJson: JsonSerializer.Serialize(new { status = CourseStatus.Proposed }),
                AfterJson: JsonSerializer.Serialize(new { status = next, notes = input.Notes?.Trim() }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private async Task<string?> ValidateAsync(CourseProposalInput input, long centerId,
        long deanUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.TitleAr) || input.TitleAr.Trim().Length > 300 ||
            input.TitleEn?.Trim().Length > 300) return "PROPOSAL_TITLE_INVALID";
        if (string.IsNullOrWhiteSpace(input.ObjectivesAr)) return "PROPOSAL_OBJECTIVES_INVALID";
        if (input.TotalHours <= 0 || decimal.Round(input.TotalHours, 2) != input.TotalHours)
            return "PROPOSAL_HOURS_INVALID";
        if (input.PlannedStartDate == default || input.PlannedEndDate < input.PlannedStartDate)
            return "PROPOSAL_DATES_INVALID";
        if (input.AudienceType is not (AudienceType.Faculty or AudienceType.Named) ||
            input.MaxParticipants is <= 0 || !Enum.IsDefined(input.DeliveryMode))
            return "PROPOSAL_INVALID";
        if (!await db.TrainingTracks.AnyAsync(x => x.Id == input.TrackId && x.IsActive, ct))
            return "TRACK_NOT_FOUND";
        // Faculty for a dean, administrative center for a center manager (scope checked by the caller).
        if (!await db.Centers.AnyAsync(x => x.Id == centerId && x.IsActive, ct))
            return "DEAN_FACULTY_REQUIRED";
        if (input.InternalInstructorUserId is long instructorId)
        {
            if (input.ExternalNameAr is not null || input.ExternalNameEn is not null ||
                input.ExternalOrgAr is not null || input.ExternalOrgEn is not null ||
                input.ExternalEmail is not null || input.CouncilLetterFileId is not null ||
                input.InstructorCvFileId is not null)
                return "INSTRUCTOR_AMBIGUOUS";
            bool member = await db.Users.AnyAsync(x => x.Id == instructorId && x.IsActive &&
                x.AccountType == AccountType.Employee && x.Employee != null &&
                x.Employee.CenterId == centerId &&
                x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus, ct);
            return member ? null : "INSTRUCTOR_NOT_IN_FACULTY";
        }
        if (string.IsNullOrWhiteSpace(input.ExternalNameAr) ||
            input.ExternalNameAr.Trim().Length > 300 ||
            input.ExternalNameEn?.Trim().Length > 300 ||
            string.IsNullOrWhiteSpace(input.ExternalOrgAr) ||
            input.ExternalOrgAr.Trim().Length > 300 ||
            input.ExternalOrgEn?.Trim().Length > 300 ||
            string.IsNullOrWhiteSpace(input.ExternalEmail) ||
            input.ExternalEmail.Trim().Length > 254 ||
            !new EmailAddressAttribute().IsValid(input.ExternalEmail))
            return "EXTERNAL_INSTRUCTOR_INVALID";
        bool requiresFiles = await settings.GetAsync<bool>("courses.external_requires_letter_and_cv", ct);
        if (requiresFiles && (input.CouncilLetterFileId is null || input.InstructorCvFileId is null))
            return "EXTERNAL_FILES_REQUIRED";
        if (input.CouncilLetterFileId is long letter &&
            !await ValidFileAsync(letter, deanUserId, FileKind.ProposalCouncilLetter, ["application/pdf"], ct))
            return "COUNCIL_LETTER_INVALID";
        if (input.InstructorCvFileId is long cv &&
            !await ValidFileAsync(cv, deanUserId, FileKind.InstructorCv,
                ["application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"], ct))
            return "INSTRUCTOR_CV_INVALID";
        return null;
    }

    private Task<bool> ValidFileAsync(long fileId, long actorId, FileKind kind,
        string[] mimeTypes, CancellationToken ct) => db.StoredFiles.AnyAsync(x =>
            x.Id == fileId && x.UploadedBy == actorId && x.IsActive && x.Kind == kind &&
            mimeTypes.Contains(x.MimeType), ct);

    private async Task<bool> IsDeanOfAsync(long userId, long centerId, CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(userId, ct);
        return effective.IsUnitHead && effective.DeanScopeCenterIds.Contains(centerId);
    }

    private async Task ApplyInputAsync(Course course, CourseProposalInput input, CancellationToken ct)
    {
        course.TitleAr = input.TitleAr.Trim();
        course.TitleEn = Clean(input.TitleEn);
        course.TrackId = input.TrackId;
        course.DeliveryMode = input.DeliveryMode;
        course.ObjectivesAr = input.ObjectivesAr.Trim();
        course.ObjectivesEn = Clean(input.ObjectivesEn);
        course.TargetAudienceAr = input.TargetAudienceAr?.Trim() ?? string.Empty;
        course.TargetAudienceEn = Clean(input.TargetAudienceEn);
        course.TotalHours = input.TotalHours;
        course.PlannedStartDate = input.PlannedStartDate;
        course.PlannedEndDate = input.PlannedEndDate;
        course.AudienceType = input.AudienceType;
        course.TargetCenterId = input.AudienceType == AudienceType.Faculty ? course.ProposingCenterId : null;
        course.RegistrationDeadline = input.AudienceType == AudienceType.Faculty ? input.RegistrationDeadline : null;
        course.MaxParticipants = input.MaxParticipants;
        course.HasExam = input.HasExam;
        course.InstructorKind = input.InternalInstructorUserId.HasValue ? InstructorKind.Internal : InstructorKind.ExternalNameOnly;
        course.InstructorUserId = input.InternalInstructorUserId;
        course.ManagedByQuality = !input.InternalInstructorUserId.HasValue;
        course.InstructorNameAr = input.InternalInstructorUserId is long id
            ? await db.Users.Where(x => x.Id == id).Select(x => x.FullNameAr).SingleAsync(ct)
            : input.ExternalNameAr!.Trim();
        course.InstructorNameEn = input.InternalInstructorUserId is long englishId
            ? await db.Users.Where(x => x.Id == englishId).Select(x => x.FullNameEn).SingleAsync(ct)
            : Clean(input.ExternalNameEn);
        course.InstructorOrgAr = input.InternalInstructorUserId.HasValue ? null : input.ExternalOrgAr!.Trim();
        course.InstructorOrgEn = input.InternalInstructorUserId.HasValue ? null : Clean(input.ExternalOrgEn);
        course.SearchText = ArabicSearchNormalizer.Normalize($"{course.CourseNo} {course.TitleAr} {course.TitleEn} {course.InstructorNameAr}");
    }

    private async Task AddSubmissionAsync(Course course, long deanUserId,
        CourseProposalInput input, CancellationToken ct)
    {
        db.CourseProposalRevisions.Add(new CourseProposalRevision
        {
            CourseId = course.Id,
            RevisionNo = course.CurrentRevisionNo,
            SnapshotJson = JsonSerializer.Serialize(input),
            SubmittedAt = clock.UtcNow,
            SubmittedBy = deanUserId,
            Decision = ProposalDecision.Pending
        });
        db.CourseStatusHistorys.Add(new CourseStatusHistory
        {
            CourseId = course.Id,
            FromStatus = course.CurrentRevisionNo == 1 ? null : CourseStatus.Returned,
            ToStatus = CourseStatus.Proposed,
            ChangedBy = deanUserId,
            ChangedAt = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task LinkFilesAsync(Course course, CourseProposalInput input,
        long actorUserId, CancellationToken ct)
    {
        foreach (var (fileId, purpose) in new[]
        {
            (input.CouncilLetterFileId, "ProposalCouncilLetter"),
            (input.InstructorCvFileId, "InstructorCv")
        })
        {
            if (fileId is null) continue;
            bool owned = await db.StoredFiles.AnyAsync(x => x.Id == fileId &&
                x.UploadedBy == actorUserId && x.IsActive, ct);
            if (!owned) throw new InvalidOperationException("Proposal attachment is not owned by the submitter.");
            db.CourseFiles.Add(new CourseFile
            {
                CourseId = course.Id,
                FileId = fileId.Value,
                Purpose = purpose,
                RevisionNo = course.CurrentRevisionNo,
                CreatedAt = clock.UtcNow,
                CreatedByUserId = actorUserId,
                IsActive = true
            });
            var vault = await db.VaultDocuments.SingleOrDefaultAsync(x =>
                x.FileId == fileId.Value && x.IsActive, ct);
            if (vault is not null)
            {
                vault.RelatedCourseId = course.Id;
                vault.RelatedCenterId = course.ProposingCenterId;
            }
        }
    }

    private async Task<string?> ValidateApprovalAsync(Course course,
        CourseProposalRevision revision, CourseProposalDecisionInput input,
        long actorUserId, CancellationToken ct)
    {
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman")).DateTime);
        if (input.EvaluationEndDate is null || input.EvaluationEndDate < today ||
            input.AudienceType is null || !Enum.IsDefined(input.AudienceType.Value) ||
            input.MaxParticipants is <= 0 || input.HasExam is null)
            return "APPROVAL_INVALID";
        if (input.AudienceType == AudienceType.Faculty &&
            (input.TargetCenterId is null || !await db.Centers.AnyAsync(x =>
                x.Id == input.TargetCenterId && x.IsActive, ct)))
            return "TARGET_FACULTY_REQUIRED";
        if (course.InstructorKind == InstructorKind.Internal)
            return input.ExternalDecision is null ? null : "EXTERNAL_DECISION_NOT_ALLOWED";
        if (input.ExternalDecision is null || !Enum.IsDefined(input.ExternalDecision.Value))
            return "EXTERNAL_DECISION_REQUIRED";
        if (input.ExternalDecision == ExternalInstructorDecision.ExistingAccount)
        {
            if (input.ExternalInstructorUserId is not long id ||
                !await db.Users.AnyAsync(x => x.Id == id && x.IsActive &&
                    x.AccountType == AccountType.ExternalInstructor, ct))
                return "EXTERNAL_ACCOUNT_NOT_FOUND";
        }
        else if (input.ExternalInstructorUserId is not null)
            return "EXTERNAL_ACCOUNT_INVALID";
        if (input.ExternalDecision == ExternalInstructorDecision.RequestNewAccount)
        {
            var proposal = JsonSerializer.Deserialize<CourseProposalInput>(revision.SnapshotJson);
            if (proposal is null || string.IsNullOrWhiteSpace(proposal.ExternalEmail))
                return "EXTERNAL_INSTRUCTOR_INVALID";
        }
        return null;
    }

    private void ApplyExternalDecision(Course course,
        CourseProposalRevision revision, CourseProposalDecisionInput input,
        long actorUserId)
    {
        if (course.InstructorKind == InstructorKind.Internal) return;
        switch (input.ExternalDecision)
        {
            case ExternalInstructorDecision.ExistingAccount:
                course.InstructorKind = InstructorKind.ExternalAccount;
                course.InstructorUserId = input.ExternalInstructorUserId;
                course.ManagedByQuality = false;
                break;
            case ExternalInstructorDecision.RequestNewAccount:
                var proposal = JsonSerializer.Deserialize<CourseProposalInput>(revision.SnapshotJson)!;
                db.ExternalInstructorAccountRequests.Add(new ExternalInstructorAccountRequest
                {
                    CourseId = course.Id,
                    RequestedByUserId = actorUserId,
                    FullNameAr = proposal.ExternalNameAr!.Trim(),
                    FullNameEn = Clean(proposal.ExternalNameEn),
                    OrganizationAr = proposal.ExternalOrgAr!.Trim(),
                    OrganizationEn = Clean(proposal.ExternalOrgEn),
                    Email = proposal.ExternalEmail!.Trim().ToLowerInvariant(),
                    Status = ExternalAccountRequestStatus.PendingAdmin,
                    ForwardedBy = actorUserId,
                    ForwardedAt = clock.UtcNow,
                    CreatedAt = clock.UtcNow,
                    CreatedByUserId = actorUserId
                });
                course.ManagedByQuality = true;
                break;
            case ExternalInstructorDecision.ManagedByQuality:
                course.ManagedByQuality = true;
                break;
        }
    }

    private async Task NotifyQualityAsync(Course course, CancellationToken ct)
    {
        var recipients = await db.UserRoles.AsNoTracking().Where(x => x.IsActive &&
            x.Role!.Code == RoleCode.Quality && x.User!.IsActive)
            .Select(x => x.UserId).Distinct().ToListAsync(ct);
        foreach (long recipient in recipients)
            AddNotification(recipient, NotificationType.ProposalSubmitted,
                "مقترح دورة جديد", "New course proposal", course);
    }

    private async Task NotifyDecisionAsync(Course course, CourseStatus status,
        CancellationToken ct)
    {
        if (course.ProposedByUserId is long deanId)
            AddNotification(deanId, NotificationType.ProposalDecision,
                "قرار على مقترح دورة", "Course proposal decision", course);
        if (status == CourseStatus.AwaitingAccount)
        {
            var admins = await db.UserRoles.AsNoTracking().Where(x => x.IsActive &&
                x.Role!.Code == RoleCode.Admin && x.User!.IsActive)
                .Select(x => x.UserId).Distinct().ToListAsync(ct);
            foreach (long id in admins)
                db.Notifications.Add(new Notification
                {
                    UserId = id,
                    Type = NotificationType.ExtInstructorRequested,
                    TitleAr = "طلب إنشاء حساب مدرب خارجي",
                    TitleEn = "External instructor account requested",
                    BodyAr = course.TitleAr,
                    BodyEn = course.TitleEn,
                    LinkUrl = "/admin/users",
                    RelatedEntityType = "Course",
                    RelatedEntityId = course.Id,
                    CreatedAt = clock.UtcNow,
                    IsActive = true
                });
        }
        if (status == CourseStatus.Approved && course.InstructorUserId is long instructorId)
            AddNotification(instructorId, NotificationType.CourseApproved,
                "اعتماد دورة تدريبية", "Course approved", course);
        if (status == CourseStatus.Approved && course.ManagedByQuality)
        {
            var qualityIds = await db.UserRoles.AsNoTracking().Where(x => x.IsActive &&
                x.Role!.Code == RoleCode.Quality && x.User!.IsActive)
                .Select(x => x.UserId).Distinct().ToListAsync(ct);
            foreach (long id in qualityIds)
                AddNotification(id, NotificationType.CourseApproved,
                    "دورة تُدار من الجودة", "Quality-managed course", course);
        }
    }

    private void AddNotification(long userId, NotificationType type,
        string titleAr, string titleEn, Course course) => db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            TitleAr = titleAr,
            TitleEn = titleEn,
            BodyAr = course.TitleAr,
            BodyEn = course.TitleEn,
            LinkUrl = $"/courses/{course.Id}",
            RelatedEntityType = "Course",
            RelatedEntityId = course.Id,
            CreatedAt = clock.UtcNow,
            IsActive = true
        });

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

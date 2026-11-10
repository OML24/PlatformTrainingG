using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Common.Security;
using ZuLms.Domain.Entities.Files;
using ZuLms.Domain.Entities.Needs;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Needs;

public sealed class NeedService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings, IAuditLogger audit)
    : INeedService
{
    public async Task<Result<NeedLookupsDto>> LookupsAsync(long actorUserId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "need.submit", ct))
            return Result<NeedLookupsDto>.Failure("FORBIDDEN");
        long? centerId = await DeanCenterAsync(actorUserId, ct);
        if (centerId is null) return Result<NeedLookupsDto>.Failure("NEED_DEAN_REQUIRED");
        var tracks = await db.TrainingTracks.AsNoTracking()
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.NameAr)
            .Select(x => new NeedOptionDto(x.Id, x.NameAr, x.NameEn)).ToListAsync(ct);
        var faculties = await db.Centers.AsNoTracking()
            .Where(x => x.IsActive && x.IsFaculty && x.Id != centerId.Value)
            .OrderBy(x => x.NameAr)
            .Select(x => new NeedOptionDto(x.Id, x.NameAr, x.NameEn)).ToListAsync(ct);
        return Result<NeedLookupsDto>.Success(new(tracks, faculties));
    }

    public async Task<Result<IReadOnlyList<NeedInstructorOptionDto>>> SearchInstructorsAsync(
        long actorUserId, long facultyId, string? search, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "need.submit", ct))
            return Result<IReadOnlyList<NeedInstructorOptionDto>>.Failure("FORBIDDEN");
        long? centerId = await DeanCenterAsync(actorUserId, ct);
        if (centerId is null || facultyId <= 0 || facultyId == centerId.Value ||
            !await db.Centers.AnyAsync(x => x.Id == facultyId && x.IsActive && x.IsFaculty, ct))
            return Result<IReadOnlyList<NeedInstructorOptionDto>>.Failure("FORBIDDEN");
        string term = ArabicSearchNormalizer.Normalize(search);
        var query = db.Users.AsNoTracking().Where(x => x.IsActive &&
            x.AccountType == AccountType.Employee && x.Employee != null &&
            x.Employee.IsAcademic && x.Employee.CenterId == facultyId &&
            x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus);
        if (term.Length > 0)
            query = query.Where(x => x.SearchText.Contains(term) ||
                x.LoginName.Contains(term));
        var rows = await query.OrderBy(x => x.FullNameAr).ThenBy(x => x.Id)
            .Take(50).Select(x => new NeedInstructorOptionDto(x.Id, x.FullNameAr,
                x.FullNameEn, x.LoginName, facultyId, x.Employee!.Center!.NameAr,
                x.Employee.Center.NameEn)).ToListAsync(ct);
        return Result<IReadOnlyList<NeedInstructorOptionDto>>.Success(rows);
    }

    public async Task<Result<long>> CreateAsync(long actorUserId, NeedInput input,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "need.submit", ct))
            return Result<long>.Failure("FORBIDDEN");
        long? centerId = await DeanCenterAsync(actorUserId, ct);
        if (centerId is null) return Result<long>.Failure("NEED_DEAN_REQUIRED");
        string? error = await ValidateAsync(input, actorUserId, ct);
        if (error is not null) return Result<long>.Failure(error);
        DateTimeOffset now = clock.UtcNow;
        return await db.InSerializableTransactionAsync(async () =>
        {
            var need = new TrainingNeed
            {
                CenterId = centerId!.Value,
                SubmittedBy = actorUserId,
                TitleAr = input.TitleAr.Trim(),
                DescriptionAr = input.DescriptionAr.Trim(),
                LetterFileId = input.LetterFileId,
                Status = NeedStatus.Submitted,
                CurrentRevision = 1,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = actorUserId
            };
            db.TrainingNeeds.Add(need);
            await db.SaveChangesAsync(ct);
            db.TrainingNeedEvents.Add(new TrainingNeedEvent
            {
                NeedId = need.Id,
                Event = "Submitted",
                ByUserId = actorUserId,
                At = now
            });
            await NotifyQualityAsync(need, "احتياج تدريبي جديد",
                "New training need submitted", now, ct);
            await audit.LogAsync(new AuditEntry(AuditCategory.Need, "need.submit",
                "رفع احتياج تدريبي", "Training need submitted", "TrainingNeed",
                need.Id, need.TitleAr, ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result<long>.Success(need.Id);
        }, ct);
    }

    public async Task<Result> ResubmitAsync(long actorUserId, long needId,
        NeedInput input, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "need.resubmit", ct))
            return Result.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        if (!effective.HasMode(RoleCode.Dean) ||
            !effective.Keys.Contains("need.resubmit"))
            return Result.Failure("FORBIDDEN");
        string? error = await ValidateAsync(input, actorUserId, ct);
        if (error is not null) return Result.Failure(error);
        DateTimeOffset now = clock.UtcNow;
        return await db.InSerializableTransactionAsync(async () =>
        {
            var need = await db.TrainingNeeds.SingleOrDefaultAsync(x => x.Id == needId, ct);
            if (need is null) return Result.Failure("NEED_NOT_FOUND");
            if (need.SubmittedBy != actorUserId || !need.IsActive ||
                !effective.DeanScopeCenterIds.Contains(need.CenterId))
                return Result.Failure("FORBIDDEN");
            if (need.Status != NeedStatus.Returned)
                return Result.Failure("NEED_NOT_RETURNED");
            need.TitleAr = input.TitleAr.Trim(); need.TitleEn = null;
            need.DescriptionAr = input.DescriptionAr.Trim();
            need.DescriptionEn = null;
            need.TrackId = null;
            need.ExpectedParticipants = null;
            need.PreferredTimeframeAr = null;
            need.RequestedInstructorUserId = null;
            need.RequestedInstructorNoteAr = null;
            need.LetterFileId = input.LetterFileId;
            need.Status = NeedStatus.Submitted;
            need.CurrentRevision++;
            need.LastReturnNotes = null;
            need.UpdatedAt = now; need.UpdatedByUserId = actorUserId;
            db.TrainingNeedEvents.Add(new TrainingNeedEvent
            {
                NeedId = need.Id,
                Event = "Resubmitted",
                ByUserId = actorUserId,
                At = now
            });
            await NotifyQualityAsync(need, "إعادة إرسال احتياج تدريبي",
                "Training need resubmitted", now, ct);
            await audit.LogAsync(new AuditEntry(AuditCategory.Need, "need.resubmit",
                "إعادة إرسال احتياج تدريبي", "Training need resubmitted", "TrainingNeed",
                need.Id, need.TitleAr, ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result> DecideAsync(long actorUserId, long needId, bool approve,
        string? notes, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "need.review", ct))
            return Result.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        if (!effective.HasMode(RoleCode.Quality) && !effective.HasMode(RoleCode.Admin))
            return Result.Failure("FORBIDDEN");
        if (!approve && string.IsNullOrWhiteSpace(notes))
            return Result.Failure("REASON_REQUIRED");
        DateTimeOffset now = clock.UtcNow;
        return await db.InSerializableTransactionAsync(async () =>
        {
            var need = await db.TrainingNeeds.SingleOrDefaultAsync(x => x.Id == needId, ct);
            if (need is null) return Result.Failure("NEED_NOT_FOUND");
            if (need.Status != NeedStatus.Submitted)
                return Result.Failure("NEED_NOT_PENDING");
            need.Status = approve ? NeedStatus.Approved : NeedStatus.Returned;
            need.LastReturnNotes = approve ? null : notes!.Trim();
            need.UpdatedAt = now; need.UpdatedByUserId = actorUserId;
            db.TrainingNeedEvents.Add(new TrainingNeedEvent
            {
                NeedId = need.Id,
                Event = approve ? "Approved" : "Returned",
                Notes = approve ? Clean(notes) : notes!.Trim(),
                ByUserId = actorUserId,
                At = now
            });
            db.Notifications.Add(new Notification
            {
                UserId = need.SubmittedBy,
                Type = NotificationType.NeedDecision,
                TitleAr = approve ? "اعتماد احتياج تدريبي" : "إعادة احتياج تدريبي",
                TitleEn = approve ? "Training need approved" : "Training need returned",
                BodyAr = approve ? need.TitleAr : $"{need.TitleAr} — {notes!.Trim()}",
                BodyEn = need.TitleEn ?? need.TitleAr,
                LinkUrl = "/dean/needs",
                RelatedEntityType = "TrainingNeed",
                RelatedEntityId = need.Id,
                CreatedAt = now,
                IsActive = true
            });
            await audit.LogAsync(new AuditEntry(AuditCategory.Need,
                approve ? "need.approve" : "need.return",
                approve ? "اعتماد احتياج تدريبي" : "إعادة احتياج تدريبي",
                approve ? "Training need approved" : "Training need returned",
                "TrainingNeed", need.Id, need.TitleAr,
                AfterJson: System.Text.Json.JsonSerializer.Serialize(
                    new { approve, notes = Clean(notes) }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    public async Task<Result<NeedPageDto>> ListAsync(long actorUserId, string? search,
        NeedStatus? status, int page, CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool reviewer = (effective.HasMode(RoleCode.Admin) || effective.HasMode(RoleCode.Quality)) &&
            effective.Keys.Contains("need.review");
        long[] deanCenters = effective.HasMode(RoleCode.Dean) &&
            effective.Keys.Contains("need.submit")
            ? effective.DeanScopeCenterIds.ToArray() : [];
        bool dean = deanCenters.Length > 0;
        if (!reviewer && !dean) return Result<NeedPageDto>.Failure("FORBIDDEN");
        bool admin = effective.HasMode(RoleCode.Admin);
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (page < 1) page = 1;
        string raw = (search ?? string.Empty).Trim();
        var query = db.TrainingNeeds.AsNoTracking();
        if (!reviewer) query = query.Where(x => deanCenters.Contains(x.CenterId));
        if (raw.Length > 0)
            query = query.Where(x => x.TitleAr.Contains(raw) ||
                (x.TitleEn != null && x.TitleEn.Contains(raw)));
        if (status is { } value) query = query.Where(x => x.Status == value);
        int total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new NeedRowDto(x.Id, x.TitleAr,
                x.Center!.NameAr,
                !admin && x.SubmittedByUser!.AccountType == AccountType.Employee &&
                (x.SubmittedByUser!.Employee == null ||
                 x.SubmittedByUser.Employee.EmpStatus != UniversityOrganizationRules.ActiveEmployeeStatus)
                    ? "—" : x.SubmittedByUser!.FullNameAr,
                x.Status,
                x.CurrentRevision, x.CreatedAt, x.LetterFileId))
            .ToListAsync(ct);
        return Result<NeedPageDto>.Success(new(page, pageSize, total, rows));
    }

    public async Task<Result<NeedDetailsDto>> GetAsync(long actorUserId, long needId,
        CancellationToken ct)
    {
        var need = await db.TrainingNeeds.AsNoTracking()
            .Where(x => x.Id == needId)
            .Select(x => new
            {
                x.Id,
                x.TitleAr,
                x.TitleEn,
                x.DescriptionAr,
                x.DescriptionEn,
                x.TrackId,
                TrackNameAr = x.Track != null ? x.Track.NameAr : null,
                x.ExpectedParticipants,
                x.PreferredTimeframeAr,
                x.RequestedInstructorUserId,
                RequestedInstructorCenterId = x.RequestedInstructorUser != null &&
                    x.RequestedInstructorUser.Employee != null
                    ? x.RequestedInstructorUser.Employee.CenterId : null,
                RequestedInstructorNameAr = x.RequestedInstructorUser != null
                    ? x.RequestedInstructorUser.FullNameAr : null,
                RequestedInstructorStatus = x.RequestedInstructorUser != null &&
                    x.RequestedInstructorUser.Employee != null
                    ? x.RequestedInstructorUser.Employee.EmpStatus : null,
                x.RequestedInstructorNoteAr,
                x.LetterFileId,
                CenterNameAr = x.Center!.NameAr,
                DeanNameAr = x.SubmittedByUser!.FullNameAr,
                DeanStatus = x.SubmittedByUser.Employee != null
                    ? x.SubmittedByUser.Employee.EmpStatus : null,
                x.Status,
                x.CurrentRevision,
                x.LastReturnNotes,
                x.CreatedAt,
                x.SubmittedBy,
                x.CenterId
            }).SingleOrDefaultAsync(ct);
        if (need is null) return Result<NeedDetailsDto>.Failure("NEED_NOT_FOUND");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool reviewer = (effective.HasMode(RoleCode.Admin) || effective.HasMode(RoleCode.Quality)) &&
            effective.Keys.Contains("need.review");
        bool dean = ScopeGuard.AllowsDeanCenterRead(effective, "need.submit", need.CenterId);
        if (!reviewer && !dean) return Result<NeedDetailsDto>.Failure("FORBIDDEN");
        bool admin = effective.HasMode(RoleCode.Admin);
        bool hideInstructor = !admin && need.RequestedInstructorUserId.HasValue &&
            need.RequestedInstructorStatus != UniversityOrganizationRules.ActiveEmployeeStatus;
        var events = await db.TrainingNeedEvents.AsNoTracking()
            .Where(x => x.NeedId == needId).OrderBy(x => x.At).ThenBy(x => x.Id)
            .Select(x => new NeedEventDto(x.Event, x.Notes,
                x.ByUser != null && (admin || x.ByUser.AccountType != AccountType.Employee ||
                    x.ByUser.Employee != null &&
                    x.ByUser.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus)
                    ? x.ByUser.FullNameAr : "—", x.At))
            .ToListAsync(ct);
        return Result<NeedDetailsDto>.Success(new NeedDetailsDto(need.Id, need.TitleAr,
            need.TitleEn, need.DescriptionAr, need.DescriptionEn, need.TrackId,
            need.TrackNameAr, need.ExpectedParticipants, need.PreferredTimeframeAr,
            hideInstructor ? null : need.RequestedInstructorUserId,
            hideInstructor ? null : need.RequestedInstructorCenterId,
            hideInstructor ? null : need.RequestedInstructorNameAr,
            need.RequestedInstructorNoteAr, need.LetterFileId, need.CenterNameAr,
            !admin && need.DeanStatus != UniversityOrganizationRules.ActiveEmployeeStatus
                ? "—" : need.DeanNameAr,
            need.Status, need.CurrentRevision, need.LastReturnNotes,
            need.CreatedAt, events));
    }

    public async Task<Result> DeactivateAsync(long actorUserId, long needId,
        string reason, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "need.review", ct))
            return Result.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        if (!effective.HasMode(RoleCode.Admin))
            return Result.Failure("FORBIDDEN");
        if (string.IsNullOrWhiteSpace(reason)) return Result.Failure("REASON_REQUIRED");
        DateTimeOffset now = clock.UtcNow;
        return await db.InSerializableTransactionAsync(async () =>
        {
            var need = await db.TrainingNeeds.SingleOrDefaultAsync(x => x.Id == needId, ct);
            if (need is null) return Result.Failure("NEED_NOT_FOUND");
            if (!need.IsActive) return Result.Failure("NEED_ALREADY_DISABLED");
            need.IsActive = false;
            need.DeactivatedAt = now; need.DeactivatedByUserId = actorUserId;
            need.DeactivationReason = reason.Trim();
            db.TrainingNeedEvents.Add(new TrainingNeedEvent
            {
                NeedId = need.Id,
                Event = "Deactivated",
                Notes = reason.Trim(),
                ByUserId = actorUserId,
                At = now
            });
            await audit.LogAsync(new AuditEntry(AuditCategory.Need, "need.deactivate",
                "تعطيل احتياج تدريبي", "Training need deactivated", "TrainingNeed",
                need.Id, need.TitleAr,
                AfterJson: System.Text.Json.JsonSerializer.Serialize(
                    new { reason = reason.Trim() }),
                ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }

    private async Task<string?> ValidateAsync(NeedInput input, long actorUserId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.TitleAr) || input.TitleAr.Trim().Length > 300 ||
            string.IsNullOrWhiteSpace(input.DescriptionAr))
            return "NEED_INVALID";
        if (input.LetterFileId is long letter)
        {
            bool valid = await db.StoredFiles.AnyAsync(x => x.Id == letter &&
                x.UploadedBy == actorUserId && x.IsActive &&
                x.Kind == FileKind.NeedLetter && x.MimeType == "application/pdf", ct);
            if (!valid) return "NEED_LETTER_INVALID";
        }
        else return "NEED_LETTER_REQUIRED";
        return null;
    }

    private async Task<long?> DeanCenterAsync(long actorUserId, CancellationToken ct)
    {
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        if (!effective.HasMode(RoleCode.Dean) ||
            !effective.Keys.Contains("need.submit")) return null;
        return effective.DeanScopeCenterIds.Order().Cast<long?>().FirstOrDefault();
    }

    private async Task NotifyQualityAsync(TrainingNeed need, string titleAr,
        string titleEn, DateTimeOffset now, CancellationToken ct)
    {
        string? centerName = await db.Centers.AsNoTracking()
            .Where(x => x.Id == need.CenterId).Select(x => x.NameAr)
            .SingleOrDefaultAsync(ct);
        var qualityUsers = await db.UserRoles.AsNoTracking()
            .Where(x => x.IsActive && x.Role!.Code == RoleCode.Quality && x.User!.IsActive)
            .Select(x => x.UserId).Distinct().ToListAsync(ct);
        foreach (long userId in qualityUsers)
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = NotificationType.NeedSubmitted,
                TitleAr = titleAr,
                TitleEn = titleEn,
                BodyAr = $"{need.TitleAr} — {centerName}",
                BodyEn = need.TitleEn ?? need.TitleAr,
                LinkUrl = "/quality/needs",
                RelatedEntityType = "TrainingNeed",
                RelatedEntityId = need.Id,
                CreatedAt = now,
                IsActive = true
            });
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

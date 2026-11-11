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

public sealed class DirectCourseService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings, IAuditLogger audit,
    IEligibilityRecalculationService recalculation)
    : IDirectCourseService
{
    public async Task<Result<DirectCourseLookupsDto>> LookupsAsync(long actorUserId,
        CancellationToken ct)
    {
        if (!await CanCreateAsync(actorUserId, ct))
            return Result<DirectCourseLookupsDto>.Failure("FORBIDDEN");
        var faculties = await db.Centers.AsNoTracking()
            .Where(x => x.IsFaculty && x.IsActive).OrderBy(x => x.NameAr)
            .Select(x => new DeanCenterOptionDto(x.Id, x.NameAr)).ToListAsync(ct);
        faculties.Insert(0, new DeanCenterOptionDto(0, "الإداريون"));
        var tracks = await db.TrainingTracks.AsNoTracking()
            .Where(x => x.IsActive && !x.IsArchived)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.NameAr)
            .Select(x => new TrackOptionDto(x.Id, x.NameAr)).ToListAsync(ct);
        bool historical = await permissions.HasAsync(actorUserId,
            "course.create_historical", ct);
        return Result<DirectCourseLookupsDto>.Success(new(faculties, tracks, historical));
    }

    public async Task<Result<IReadOnlyList<InstructorOptionDto>>> SearchInstructorsAsync(
        long actorUserId, bool external, string? search, CancellationToken ct)
    {
        if (!await CanCreateAsync(actorUserId, ct))
            return Result<IReadOnlyList<InstructorOptionDto>>.Failure("FORBIDDEN");
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);
        string term = ArabicSearchNormalizer.Normalize(search);
        var query = db.Users.AsNoTracking().Where(x => x.IsActive &&
            x.AccountType == (external ? AccountType.ExternalInstructor : AccountType.Employee) &&
            (external || admin || x.Employee != null &&
                x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus));
        if (term.Length > 0)
            query = query.Where(x => x.SearchText.Contains(term) || x.LoginName.Contains(term));
        var rows = await query.OrderBy(x => x.FullNameAr).ThenBy(x => x.Id).Take(30)
            .Select(x => new InstructorOptionDto(x.Id, x.FullNameAr, x.LoginName,
                null, null, admin && x.Employee != null ? x.Employee.EmpStatus : null,
                admin && x.Employee != null ? x.Employee.EmpStatusDesc : null))
            .ToListAsync(ct);
        return Result<IReadOnlyList<InstructorOptionDto>>.Success(rows);
    }

    public async Task<Result<IReadOnlyList<HistoricalEmployeeDto>>> HistoricalEmployeesAsync(
        long actorUserId, long centerId, CancellationToken ct)
    {
        if (!await CanCreateAsync(actorUserId, ct) ||
            !await permissions.HasAsync(actorUserId, "course.create_historical", ct))
            return Result<IReadOnlyList<HistoricalEmployeeDto>>.Failure("FORBIDDEN");
        if (centerId != 0 && !await db.Centers.AnyAsync(x => x.Id == centerId && x.IsFaculty && x.IsActive, ct))
            return Result<IReadOnlyList<HistoricalEmployeeDto>>.Failure("FACULTY_NOT_FOUND");
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);
        var rows = await db.Users.AsNoTracking().Where(x => x.IsActive &&
                x.AccountType == AccountType.Employee && x.Employee != null &&
                (admin || x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus) &&
                (centerId == 0 ? !x.Employee.IsAcademic : x.Employee.CenterId == centerId))
            .OrderBy(x => x.FullNameAr).ThenBy(x => x.Id)
            .Select(x => new HistoricalEmployeeDto(x.Id, x.FullNameAr, x.LoginName,
                admin ? x.Employee!.EmpStatus : null,
                admin ? x.Employee!.EmpStatusDesc : null))
            .ToListAsync(ct);
        return Result<IReadOnlyList<HistoricalEmployeeDto>>.Success(rows);
    }

    private async Task<bool> CanCreateAsync(long actorUserId, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "course.create_direct", ct))
            return false;
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        return effective.HasMode(RoleCode.Admin) || effective.HasMode(RoleCode.Quality);
    }

    public async Task<Result<long>> CreateAsync(long actorUserId, DirectCourseInput input,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "course.create_direct", ct))
            return Result<long>.Failure("FORBIDDEN");
        var effective = await permissions.GetEffectiveAsync(actorUserId, ct);
        bool admin = effective.HasMode(RoleCode.Admin);
        if (!admin && !effective.HasMode(RoleCode.Quality))
            return Result<long>.Failure("FORBIDDEN");
        if (input.IsHistorical &&
            !await permissions.HasAsync(actorUserId, "course.create_historical", ct))
            return Result<long>.Failure("FORBIDDEN");
        string? error = await ValidateAsync(input, actorUserId, ct);
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
                TitleAr = input.TitleAr.Trim(),
                TitleEn = Clean(input.TitleEn),
                TrackId = input.TrackId,
                DeliveryMode = input.DeliveryMode,
                ObjectivesAr = input.ObjectivesAr.Trim(),
                ObjectivesEn = Clean(input.ObjectivesEn),
                TargetAudienceAr = input.TargetAudienceAr?.Trim() ?? string.Empty,
                TargetAudienceEn = Clean(input.TargetAudienceEn),
                TotalHours = input.TotalHours,
                PlannedStartDate = input.PlannedStartDate,
                PlannedEndDate = input.PlannedEndDate,
                Origin = input.IsHistorical ? CourseOrigin.Historical :
                    admin ? CourseOrigin.AdminCreated : CourseOrigin.QualityCreated,
                ProposingCenterId = input.RelatedCenterId,
                AudienceType = input.AudienceType,
                TargetCenterId = input.AudienceType == AudienceType.Faculty
                    ? input.TargetCenterId : null,
                RegistrationDeadline = input.IsHistorical ? null : input.RegistrationDeadline,
                MaxParticipants = input.MaxParticipants,
                // A historical result is the authoritative attendance/exam decision.
                HasExam = input.IsHistorical ? false : input.HasExam,
                Status = input.IsHistorical ? CourseStatus.Finished : CourseStatus.Approved,
                StatusChangedAt = now,
                ApprovedBy = actorUserId,
                ApprovedAt = now,
                EvaluationEndDate = input.IsHistorical && !input.HistoricalEvaluationRequired
                    ? null : input.EvaluationEndDate,
                ImpactEndDate = input.IsHistorical ? null : input.ImpactEndDate,
                IsHistorical = input.IsHistorical,
                HistoricalNotes = input.IsHistorical ? Clean(input.HistoricalNotes) : null,
                InstructorKind = input.InstructorChoice switch
                {
                    DirectInstructorChoice.Internal => InstructorKind.Internal,
                    DirectInstructorChoice.ExistingExternal => InstructorKind.ExternalAccount,
                    DirectInstructorChoice.ManagedByQuality => InstructorKind.ManagedByQuality,
                    _ => InstructorKind.ExternalNameOnly
                },
                InstructorUserId = input.InstructorChoice is DirectInstructorChoice.Internal
                    or DirectInstructorChoice.ExistingExternal ? input.InstructorUserId : null,
                ManagedByQuality = input.InstructorChoice is DirectInstructorChoice.NewExternalAccount
                    or DirectInstructorChoice.ManagedByQuality,
                IsActive = true,
                CurrentRevisionNo = 1,
                FinalLecturesCount = input.IsHistorical ? 0 : null,
                CreatedAt = now,
                CreatedByUserId = actorUserId
            };
            if (course.InstructorUserId is long instructorId)
            {
                var instructor = await db.Users.AsNoTracking()
                    .SingleAsync(x => x.Id == instructorId, ct);
                course.InstructorNameAr = instructor.FullNameAr;
                course.InstructorNameEn = instructor.FullNameEn;
                if (input.InstructorChoice == DirectInstructorChoice.ExistingExternal)
                {
                    var profile = await db.ExternalInstructorProfiles.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.UserId == instructorId, ct);
                    course.InstructorOrgAr = profile?.OrganizationAr;
                    course.InstructorOrgEn = profile?.OrganizationEn;
                }
            }
            else if (input.InstructorChoice != DirectInstructorChoice.ManagedByQuality)
            {
                course.InstructorNameAr = input.ExternalNameAr!.Trim();
                course.InstructorNameEn = Clean(input.ExternalNameEn);
                course.InstructorOrgAr = input.ExternalOrgAr!.Trim();
                course.InstructorOrgEn = Clean(input.ExternalOrgEn);
            }
            else course.InstructorNameAr = "تديرها الجودة";
            course.SearchText = ArabicSearchNormalizer.Normalize(
                $"{course.CourseNo} {course.TitleAr} {course.TitleEn} {course.InstructorNameAr}");
            db.Courses.Add(course);
            await db.SaveChangesAsync(ct);
            db.CourseStatusHistorys.Add(new CourseStatusHistory
            {
                CourseId = course.Id,
                FromStatus = null,
                ToStatus = course.Status,
                ChangedBy = actorUserId,
                ChangedAt = now,
                Reason = input.IsHistorical ? "Historical course" : null
            });
            await LinkFilesAsync(course, input, actorUserId, now, ct);
            var historicalParticipants = new List<CourseParticipant>();
            if (input.IsHistorical)
            {
                foreach (var item in input.HistoricalParticipants!)
                {
                    var participant = new CourseParticipant
                    {
                        CourseId = course.Id,
                        UserId = item.UserId,
                        Source = ParticipantSource.HistoricalEntry,
                        AddedByUserId = actorUserId,
                        JoinedAt = now,
                        HistoricalPassed = item.Passed,
                        EvaluationExempt = !input.HistoricalEvaluationRequired,
                        IsActive = true,
                        CreatedAt = now,
                        CreatedByUserId = actorUserId
                    };
                    historicalParticipants.Add(participant);
                    db.CourseParticipants.Add(participant);
                }
            }
            if (input.InstructorChoice == DirectInstructorChoice.NewExternalAccount)
            {
                db.ExternalInstructorAccountRequests.Add(new ExternalInstructorAccountRequest
                {
                    CourseId = course.Id,
                    RequestedByUserId = actorUserId,
                    FullNameAr = input.ExternalNameAr!.Trim(),
                    FullNameEn = Clean(input.ExternalNameEn),
                    OrganizationAr = input.ExternalOrgAr!.Trim(),
                    OrganizationEn = Clean(input.ExternalOrgEn),
                    Email = input.ExternalEmail!.Trim().ToLowerInvariant(),
                    Status = ExternalAccountRequestStatus.PendingAdmin,
                    ForwardedBy = actorUserId,
                    ForwardedAt = now,
                    CreatedAt = now,
                    CreatedByUserId = actorUserId
                });
                await NotifyAdminsAsync(course, ct);
            }
            if (course.InstructorUserId is long userId)
                db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.CourseApproved,
                    TitleAr = "اعتماد دورة تدريبية",
                    TitleEn = "Course approved",
                    BodyAr = course.TitleAr,
                    BodyEn = course.TitleEn,
                    LinkUrl = $"/courses/{course.Id}",
                    RelatedEntityType = "Course",
                    RelatedEntityId = course.Id,
                    CreatedAt = now,
                    IsActive = true
                });
            await audit.LogAsync(new AuditEntry(AuditCategory.Course, "course.create_direct",
                "إنشاء دورة مباشرة", "Course created directly", "Course", course.Id,
                course.TitleAr, AfterJson: JsonSerializer.Serialize(new
                {
                    course.Status,
                    course.Origin,
                    course.IsHistorical,
                    course.InstructorKind,
                    course.AudienceType
                }), ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            foreach (var participant in historicalParticipants.Where(x => x.HistoricalPassed == true))
            {
                var result = await recalculation.RecalculateParticipantAsync(course.Id,
                    participant.Id, null, actorUserId, ct);
                if (!result.Ok) return Result<long>.Failure(result.Error!.Code);
            }
            return Result<long>.Success(course.Id);
        }, ct);
    }

    private async Task<string?> ValidateAsync(DirectCourseInput input,
        long actorUserId, CancellationToken ct)
    {
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);
        if (string.IsNullOrWhiteSpace(input.TitleAr) || input.TitleAr.Trim().Length > 300 ||
            input.TitleEn?.Trim().Length > 300 ||
            string.IsNullOrWhiteSpace(input.ObjectivesAr) ||
            input.TotalHours <= 0 || input.TotalHours > 9999.99m ||
            decimal.Round(input.TotalHours, 2) != input.TotalHours ||
            !Enum.IsDefined(input.DeliveryMode) || !Enum.IsDefined(input.AudienceType) ||
            !Enum.IsDefined(input.InstructorChoice) || input.MaxParticipants is <= 0)
            return "COURSE_INVALID";
        if (!input.IsHistorical &&
            (!input.PlannedStartDate.HasValue || !input.PlannedEndDate.HasValue ||
             input.PlannedEndDate < input.PlannedStartDate) ||
            input.IsHistorical && input.PlannedStartDate.HasValue && input.PlannedEndDate.HasValue &&
            input.PlannedEndDate < input.PlannedStartDate)
            return "COURSE_INVALID";
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman")).DateTime);
        if ((!input.IsHistorical || input.HistoricalEvaluationRequired) &&
            (!input.EvaluationEndDate.HasValue || input.EvaluationEndDate.Value == default))
            return "EVALUATION_END_REQUIRED";
        if (!input.IsHistorical &&
            (input.PlannedStartDate < today || input.EvaluationEndDate < today) ||
            input.IsHistorical && input.HistoricalEvaluationRequired &&
            input.EvaluationEndDate < today)
            return "COURSE_DATE_PAST";
        if (!input.IsHistorical && input.RegistrationDeadline is { } deadline &&
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(deadline,
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Amman")).DateTime) >= input.PlannedStartDate!.Value)
            return "REGISTRATION_DEADLINE_INVALID";
        if (input.EvaluationEndDate is { } evaluationEnd && input.PlannedEndDate is { } plannedEnd &&
            evaluationEnd < plannedEnd)
            return "EVALUATION_END_INVALID";
        if (input.IsHistorical)
        {
            if (input.PlannedStartDate >= today || input.PlannedEndDate >= today)
                return "HISTORICAL_DATE_REQUIRED";
            var historical = input.HistoricalParticipants;
            if (input.AudienceType is not (AudienceType.Faculty or AudienceType.Administrative) ||
                historical is null || historical.Count == 0 || historical.Count > 1000 ||
                historical.Any(x => x.UserId <= 0) ||
                historical.Select(x => x.UserId).Distinct().Count() != historical.Count)
                return "HISTORICAL_PARTICIPANTS_REQUIRED";
            var ids = historical.Select(x => x.UserId).ToList();
            int valid = await db.Users.CountAsync(x => ids.Contains(x.Id) && x.IsActive &&
                x.AccountType == AccountType.Employee && x.Employee != null &&
                (admin || x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus) &&
                (input.AudienceType == AudienceType.Administrative
                    ? !x.Employee.IsAcademic
                    : x.Employee.CenterId == input.TargetCenterId), ct);
            if (valid != ids.Count) return "HISTORICAL_PARTICIPANTS_INVALID";
        }
        if (!await db.TrainingTracks.AnyAsync(x => x.Id == input.TrackId && x.IsActive &&
            !x.IsArchived, ct)) return "TRACK_NOT_FOUND";
        if (input.RelatedCenterId is long related &&
            !await db.Centers.AnyAsync(x => x.Id == related && x.IsFaculty && x.IsActive, ct))
            return "RELATED_FACULTY_INVALID";
        if (input.AudienceType == AudienceType.Faculty &&
            (input.TargetCenterId is not long target ||
             !await db.Centers.AnyAsync(x => x.Id == target && x.IsFaculty && x.IsActive, ct)))
            return "TARGET_FACULTY_REQUIRED";
        if (input.InstructorChoice is DirectInstructorChoice.Internal or
            DirectInstructorChoice.ExistingExternal)
        {
            AccountType required = input.InstructorChoice == DirectInstructorChoice.Internal
                ? AccountType.Employee : AccountType.ExternalInstructor;
            if (input.InstructorUserId is not long userId ||
                !await db.Users.AnyAsync(x => x.Id == userId && x.IsActive &&
                    x.AccountType == required && (required != AccountType.Employee || admin ||
                        x.Employee != null && x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus), ct))
                return "INSTRUCTOR_INVALID";
            if (input.ExternalNameAr is not null || input.ExternalNameEn is not null ||
                input.ExternalOrgAr is not null || input.ExternalOrgEn is not null ||
                input.ExternalEmail is not null ||
                input.CouncilLetterFileId is not null || input.InstructorCvFileId is not null)
                return "INSTRUCTOR_AMBIGUOUS";
            return null;
        }
        if (input.InstructorChoice == DirectInstructorChoice.ManagedByQuality)
        {
            if (input.InstructorUserId is not null ||
                input.ExternalNameAr is not null || input.ExternalNameEn is not null ||
                input.ExternalOrgAr is not null || input.ExternalOrgEn is not null ||
                input.ExternalEmail is not null ||
                input.CouncilLetterFileId is not null || input.InstructorCvFileId is not null)
                return "INSTRUCTOR_AMBIGUOUS";
            return null;
        }
        if (input.InstructorUserId is not null ||
            string.IsNullOrWhiteSpace(input.ExternalNameAr) ||
            input.ExternalNameAr.Trim().Length > 300 ||
            string.IsNullOrWhiteSpace(input.ExternalOrgAr) ||
            input.ExternalOrgAr.Trim().Length > 300 ||
            input.ExternalNameEn?.Trim().Length > 300 ||
            input.ExternalOrgEn?.Trim().Length > 300)
            return "EXTERNAL_INSTRUCTOR_INVALID";
        if (input.InstructorChoice == DirectInstructorChoice.NewExternalAccount &&
            (string.IsNullOrWhiteSpace(input.ExternalEmail) ||
             input.ExternalEmail.Trim().Length > 254 ||
             !new EmailAddressAttribute().IsValid(input.ExternalEmail)))
            return "EXTERNAL_EMAIL_INVALID";
        bool requiresFiles = await settings.GetAsync<bool>("courses.external_requires_letter_and_cv", ct);
        if (requiresFiles &&
            (input.CouncilLetterFileId is null || input.InstructorCvFileId is null))
            return "EXTERNAL_FILES_REQUIRED";
        if (input.CouncilLetterFileId is long letter &&
            !await ValidFileAsync(letter, actorUserId, FileKind.ProposalCouncilLetter,
                ["application/pdf"], ct)) return "COUNCIL_LETTER_INVALID";
        if (input.InstructorCvFileId is long cv &&
            !await ValidFileAsync(cv, actorUserId, FileKind.InstructorCv,
                ["application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"], ct))
            return "INSTRUCTOR_CV_INVALID";
        return null;
    }

    private Task<bool> ValidFileAsync(long fileId, long actorId, FileKind kind,
        string[] mimeTypes, CancellationToken ct) => db.StoredFiles.AnyAsync(x =>
            x.Id == fileId && x.UploadedBy == actorId && x.IsActive &&
            x.Kind == kind && mimeTypes.Contains(x.MimeType), ct);

    private async Task LinkFilesAsync(Course course, DirectCourseInput input,
        long actorUserId, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var (fileId, purpose) in new[]
        {
            (input.CouncilLetterFileId, "ProposalCouncilLetter"),
            (input.InstructorCvFileId, "InstructorCv")
        })
        {
            if (fileId is null) continue;
            db.CourseFiles.Add(new CourseFile
            {
                CourseId = course.Id,
                FileId = fileId.Value,
                Purpose = purpose,
                CreatedAt = now,
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

    private async Task NotifyAdminsAsync(Course course, CancellationToken ct)
    {
        var adminIds = await db.UserRoles.AsNoTracking().Where(x => x.IsActive &&
            x.Role!.Code == RoleCode.Admin && x.User!.IsActive)
            .Select(x => x.UserId).Distinct().ToListAsync(ct);
        foreach (long id in adminIds)
            db.Notifications.Add(new Notification
            {
                UserId = id,
                Type = NotificationType.ExtInstructorRequested,
                TitleAr = "طلب حساب مدرب خارجي",
                TitleEn = "External instructor account request",
                BodyAr = course.TitleAr,
                BodyEn = course.TitleEn,
                LinkUrl = "/admin/users",
                RelatedEntityType = "Course",
                RelatedEntityId = course.Id,
                CreatedAt = clock.UtcNow,
                IsActive = true
            });
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

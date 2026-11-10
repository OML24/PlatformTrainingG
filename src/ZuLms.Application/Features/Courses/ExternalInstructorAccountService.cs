using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Courses;

public sealed class ExternalInstructorAccountService(IAppDbContext db, IClock clock,
    IPermissionService permissions, ISettingsService settings,
    IPasswordHasher hasher, IEncryptionService encryption,
    IEmailContentBuilder emailContent, IAuditLogger audit)
    : IExternalInstructorAccountService
{
    public async Task<Result<ExternalRequestPageDto>> ListPendingAsync(long actorUserId,
        int page, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.users", ct))
            return Result<ExternalRequestPageDto>.Failure("FORBIDDEN");
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (page < 1 || pageSize < 1 || page > int.MaxValue / pageSize)
            return Result<ExternalRequestPageDto>.Failure("QUERY_INVALID");
        var query = db.ExternalInstructorAccountRequests.AsNoTracking()
            .Where(x => x.Status == ExternalAccountRequestStatus.PendingAdmin);
        int total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize)
            .Take(pageSize).Select(x => new ExternalRequestDto(x.Id, x.CourseId,
                x.Course!.CourseNo, x.FullNameAr, x.OrganizationAr,
                x.Email, x.Status, x.Course.TitleAr, x.Course.Status, x.CreatedAt)).ToListAsync(ct);
        return Result<ExternalRequestPageDto>.Success(new(page, pageSize, total, rows));
    }

    public async Task<Result<string>> SuggestLoginAsync(long actorUserId,
        CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "user.create", ct))
            return Result<string>.Failure("FORBIDDEN");
        int min = await settings.GetAsync<int>("auth.external_login_min", ct);
        int max = await settings.GetAsync<int>("auth.external_login_max", ct);
        var used = (await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Select(x => x.LoginName).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        for (int number = min; number <= max; number++)
        {
            string login = number.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
            if (!used.Contains(login)) return Result<string>.Success(login);
        }
        return Result<string>.Failure("EXTERNAL_LOGIN_RANGE_FULL");
    }

    public async Task<Result<ExternalAccountCreatedDto>> CompleteAsync(long actorUserId,
        long requestId, string loginName, string? password, CancellationToken ct)
    {
        if (!await permissions.HasAsync(actorUserId, "user.create", ct))
            return Result<ExternalAccountCreatedDto>.Failure("FORBIDDEN");
        if (!await db.ExternalInstructorAccountRequests.AnyAsync(x => x.Id == requestId, ct))
            return Result<ExternalAccountCreatedDto>.Failure("EXTERNAL_REQUEST_NOT_FOUND");
        string login = LoginNameRules.NormalizeLogin(loginName);
        int min = await settings.GetAsync<int>("auth.external_login_min", ct);
        int max = await settings.GetAsync<int>("auth.external_login_max", ct);
        if (!LoginNameRules.IsExternalLogin(login) ||
            !int.TryParse(login, out int number) || number < min || number > max)
            return Result<ExternalAccountCreatedDto>.Failure("EXTERNAL_LOGIN_INVALID");
        return await db.InSerializableTransactionAsync(async () =>
        {
            var request = await db.ExternalInstructorAccountRequests.Include(x => x.Course)
                .SingleOrDefaultAsync(x => x.Id == requestId, ct);
            if (request is null) return Result<ExternalAccountCreatedDto>.Failure("EXTERNAL_REQUEST_NOT_FOUND");
            if (request.Status != ExternalAccountRequestStatus.PendingAdmin ||
                request.Course is null || !ExternalInstructorCourseStatus.CanCompleteAccountRequest(request.Course.Status) ||
                request.Course.InstructorKind != InstructorKind.ExternalNameOnly)
                return Result<ExternalAccountCreatedDto>.Failure("EXTERNAL_REQUEST_NOT_PENDING");
            if (await db.Users.IgnoreQueryFilters().AnyAsync(x => x.LoginName == login, ct))
                return Result<ExternalAccountCreatedDto>.Failure("LOGIN_EXISTS");
            var policy = await db.PasswordPolicys.AsNoTracking().SingleAsync(ct);
            var blocked = await db.BlockedPasswords.AsNoTracking()
                .Select(x => x.PasswordNormalized).ToListAsync(ct);
            string[] context = request.FullNameAr.Split(' ',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Concat([request.OrganizationAr, login]).ToArray();
            string secret;
            try
            {
                secret = password is null
                    ? InitialPasswordRules.Generate(policy, blocked, context) : password;
            }
            catch (InvalidOperationException)
            {
                return Result<ExternalAccountCreatedDto>.Failure("PASSWORD_GENERATION_FAILED");
            }
            var errors = PasswordPolicyRules.Validate(secret, policy, blocked, context);
            if (errors.Count > 0)
                return Result<ExternalAccountCreatedDto>.Failure(errors[0]);
            DateTimeOffset now = clock.UtcNow;
            var user = new User
            {
                AccountType = AccountType.ExternalInstructor,
                LoginName = login,
                FullNameAr = request.FullNameAr,
                FullNameEn = request.FullNameEn,
                EmailOverride = request.Email.Trim().ToLowerInvariant(),
                PasswordHash = hasher.Hash(secret),
                PasswordEnc = encryption.Encrypt(secret),
                InitialPasswordEnc = encryption.Encrypt(secret),
                MustChangePassword = false,
                FirstLoginCompletedAt = now,
                PasswordChangedAt = now,
                PasswordChangedBy = actorUserId,
                SecurityStamp = Guid.NewGuid(),
                PreferredLanguage = UiLanguage.Ar,
                CredentialsEmailStatus = CredentialsEmailStatus.NotSent,
                SearchText = ArabicSearchNormalizer.Normalize($"{request.FullNameAr} {request.FullNameEn} {login}"),
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = actorUserId
            };
            db.Users.Add(user);
            long roleId = await db.Roles.Where(x => x.Code == RoleCode.ExternalInstructor)
                .Select(x => x.Id).SingleAsync(ct);
            db.UserRoles.Add(new UserRole
            {
                User = user,
                RoleId = roleId,
                IsActive = true,
                CreatedAt = now,
                CreatedByUserId = actorUserId
            });
            long? cvFileId = await db.CourseFiles.Where(x => x.CourseId == request.CourseId &&
                x.Purpose == "InstructorCv" && x.IsActive)
                .OrderByDescending(x => x.RevisionNo).Select(x => (long?)x.FileId)
                .FirstOrDefaultAsync(ct);
            db.ExternalInstructorProfiles.Add(new ExternalInstructorProfile
            {
                User = user,
                OrganizationAr = request.OrganizationAr,
                OrganizationEn = request.OrganizationEn,
                CvFileId = cvFileId,
                CreatedFromRequestId = request.Id
            });
            request.CreatedUser = user;
            request.Status = ExternalAccountRequestStatus.Completed;
            request.HandledBy = actorUserId;
            request.HandledAt = now;
            request.UpdatedAt = now;
            request.UpdatedByUserId = actorUserId;
            CourseStatus previousCourseStatus = request.Course.Status;
            request.Course.InstructorUser = user;
            request.Course.InstructorKind = InstructorKind.ExternalAccount;
            request.Course.ManagedByQuality = false;
            if (previousCourseStatus == CourseStatus.AwaitingAccount)
            {
                request.Course.Status = ExternalInstructorCourseStatus.AfterAccountCompletion(previousCourseStatus);
                request.Course.StatusChangedAt = now;
                db.CourseStatusHistorys.Add(new ZuLms.Domain.Entities.Training.CourseStatusHistory
                {
                    CourseId = request.CourseId,
                    FromStatus = CourseStatus.AwaitingAccount,
                    ToStatus = CourseStatus.Approved,
                    ChangedBy = actorUserId,
                    ChangedAt = now,
                    Reason = "External instructor account created"
                });
            }
            request.Course.UpdatedAt = now;
            request.Course.UpdatedByUserId = actorUserId;
            var mail = emailContent.ExternalInstructorAccount(user.FullNameAr,
                login, secret, user.PreferredLanguage);
            db.EmailOutboxs.Add(new EmailOutbox
            {
                Kind = EmailKind.ExternalInstructorAccount,
                ToAddress = request.Email,
                ToName = user.FullNameAr,
                Subject = mail.Subject,
                BodyHtml = mail.BodyHtml,
                RelatedUser = user,
                Status = OutboxStatus.Pending,
                CreatedAt = now,
                NextAttemptAt = now
            });
            db.Notifications.Add(new Notification
            {
                UserId = request.RequestedByUserId,
                Type = NotificationType.ExtInstructorReady,
                TitleAr = "حساب المدرب الخارجي جاهز",
                TitleEn = "External instructor account ready",
                BodyAr = request.FullNameAr,
                BodyEn = request.FullNameEn,
                LinkUrl = $"/courses/{request.CourseId}",
                RelatedEntityType = "Course",
                RelatedEntityId = request.CourseId,
                CreatedAt = now,
                IsActive = true
            });
            if (request.Course.ProposedByUserId is long deanId)
                db.Notifications.Add(new Notification
                {
                    UserId = deanId,
                    Type = NotificationType.ProposalDecision,
                    TitleAr = "تم إنشاء حساب المدرب وتفعيل الدورة",
                    TitleEn = "Instructor account created; course activated",
                    BodyAr = request.Course.TitleAr,
                    BodyEn = request.Course.TitleEn,
                    LinkUrl = $"/courses/{request.CourseId}",
                    RelatedEntityType = "Course",
                    RelatedEntityId = request.CourseId,
                    CreatedAt = now,
                    IsActive = true
                });
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(new AuditEntry(AuditCategory.User, "user.create_external",
                "إنشاء حساب مدرب خارجي", "External instructor account created",
                "User", user.Id, request.FullNameAr, ActorUserId: actorUserId), ct);
            await db.SaveChangesAsync(ct);
            return Result<ExternalAccountCreatedDto>.Success(new(user.Id, login));
        }, ct);
    }
}

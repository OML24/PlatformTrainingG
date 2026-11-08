using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Certificates;
using ZuLms.Application.Features.Courses;
using ZuLms.Domain.Entities.Profiles;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Rules;

namespace ZuLms.Application.Features.Profiles;

public interface IProfileService
{
    Task<Result<ProfilePageDto>> DirectoryAsync(long actorUserId, string? search,
        long? centerId, int page, CancellationToken ct);
    Task<Result<ProfileDto>> GetAsync(long actorUserId, long userId, CancellationToken ct);
    Task<Result> UpdateOwnAsync(long actorUserId, string? bioAr, string? bioEn,
        string? contactMethod, string? contactValue, bool showInDirectory,
        CancellationToken ct);
}

public sealed record DirectoryRowDto(long UserId, string FullNameAr,
    string? CenterNameAr, string? DepartmentNameAr, string[] ExpertiseAr,
    int? EmpStatus, string? EmpStatusDesc);

public sealed record ProfilePageDto(int Page, int PageSize, int Total,
    IReadOnlyList<DirectoryRowDto> Items);

public sealed record ProfileDto(long UserId, string FullNameAr, string? FullNameEn,
    string? CenterNameAr, string? DepartmentNameAr, string? AcademicRank,
    string? BioAr, string? BioEn, string[] ExpertiseAr, string? ContactMethod,
    string? ContactValue, long? PhotoFileId,
    IReadOnlyList<PlatformCertificateDto> Certificates,
    IReadOnlyList<ProCertCardDto> ProCerts, int ProCertCount,
    IReadOnlyList<CourseParticipationDto> CourseParticipations);

public sealed record PlatformCertificateDto(long Id, string ReferenceNo,
    string CourseTitleAr, string CoursePeriodText, decimal CourseHours,
    DateTimeOffset IssuedAt, string VerifyUrl);

public sealed record ProCertCardDto(long Id, string Name, string IssuerName,
    string CategoryNameAr, DateOnly IssuedDate, DateOnly? ExpiryDate, bool IsExpired,
    string? VerifyUrl);
public sealed record CourseParticipationDto(long CourseId, string CourseNo,
    string CourseTitleAr, CourseStatus CourseStatus, bool IsFinal, bool Eligible,
    string? CertificateReference, IReadOnlyList<string> UnmetReasons);

public sealed class ProfileService(IAppDbContext db, ISettingsService settings,
    IEligibilitySnapshotService eligibility, IPermissionService permissions)
    : IProfileService
{
    public async Task<Result<ProfilePageDto>> DirectoryAsync(long actorUserId,
        string? search, long? centerId, int page, CancellationToken ct)
    {
        int pageSize = await settings.GetAsync<int>("ui.default_page_size", ct);
        if (page < 1) page = 1;
        string term = ArabicSearchNormalizer.Normalize(search);
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);
        var query = db.Users.AsNoTracking()
            .Where(x => x.IsActive && x.AccountType == AccountType.Employee &&
                x.Employee != null &&
                (admin || !db.MemberProfiles.Any(p => p.UserId == x.Id) ||
                 db.MemberProfiles.Any(p => p.UserId == x.Id && p.ShowInDirectory)));
        if (!admin) query = query.Where(x => x.Employee!.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus);
        if (centerId is long center)
            query = center == 0
                ? query.Where(x => !x.Employee!.IsAcademic)
                : query.Where(x => x.Employee!.CenterId == center);
        var rows = await query
            .Select(x => new DirectoryRowDto(x.Id, x.FullNameAr,
                x.Employee!.IsAcademic && x.Employee.Center != null
                    ? x.Employee.Center.NameAr : "الإداريون",
                x.Employee.Department != null ? x.Employee.Department.NameAr : null,
                db.MemberProfiles.Where(p => p.UserId == x.Id)
                    .Select(p => p.ExpertiseAr).FirstOrDefault() ?? Array.Empty<string>(),
                admin ? x.Employee.EmpStatus : null,
                admin ? x.Employee.EmpStatusDesc : null))
            .ToListAsync(ct);
        if (term.Length > 0)
        {
            rows = rows.Where(x =>
                ArabicSearchNormalizer.Normalize(x.FullNameAr).Contains(term) ||
                ArabicSearchNormalizer.Normalize(x.CenterNameAr ?? string.Empty).Contains(term) ||
                x.ExpertiseAr.Any(e =>
                    ArabicSearchNormalizer.Normalize(e).Contains(term))).ToList();
        }
        int total = rows.Count;
        var items = rows.OrderBy(x => x.FullNameAr)
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Result<ProfilePageDto>.Success(new(page, pageSize, total, items));
    }

    public async Task<Result<ProfileDto>> GetAsync(long actorUserId, long userId,
        CancellationToken ct)
    {
        bool admin = (await permissions.GetEffectiveAsync(actorUserId, ct)).HasMode(RoleCode.Admin);
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive &&
                x.AccountType == AccountType.Employee && x.Employee != null &&
                (admin || x.Employee.EmpStatus == UniversityOrganizationRules.ActiveEmployeeStatus))
            .Select(x => new
            {
                x.Id, x.FullNameAr, x.FullNameEn,
                CenterName = x.Employee != null && !x.Employee.IsAcademic
                    ? "الإداريون" : x.Employee != null && x.Employee.Center != null
                        ? x.Employee.Center.NameAr : null,
                DepartmentName = x.Employee != null && x.Employee.Department != null
                    ? x.Employee.Department.NameAr : null,
                JobTitle = x.Employee != null ? x.Employee.MainJobDesc : null
            }).SingleOrDefaultAsync(ct);
        if (user is null) return Result<ProfileDto>.Failure("USER_NOT_FOUND");
        var memberProfile = await db.MemberProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        string verifyBase = await CertificateVerificationUrl.BaseAsync(settings, ct);
        var platformCerts = await db.Certificates.AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == CertificateStatus.Issued &&
                x.IsActive && !x.HiddenByCourseDisable)
            .OrderByDescending(x => x.IssuedAt)
            .Select(x => new PlatformCertificateDto(x.Id, x.ReferenceNo,
                x.CourseTitleAr, x.CoursePeriodText, x.CourseHours, x.IssuedAt,
                CertificateVerificationUrl.Build(verifyBase, x.ReferenceNo)))
            .ToListAsync(ct);
        bool showExpired = await settings.GetAsync<bool>(
            "procert.show_expired_in_profile", ct);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        var proCerts = await db.ProfessionalCertifications.AsNoTracking()
            .Where(x => x.UserId == userId && x.Status == ProCertStatus.Approved &&
                x.IsActive)
            .OrderByDescending(x => x.IssuedDate)
            .Select(x => new ProCertCardDto(x.Id, x.Name, x.IssuerName,
                x.Category!.NameAr, x.IssuedDate, x.ExpiryDate,
                x.ExpiryDate.HasValue && x.ExpiryDate.Value < today, x.VerifyUrl))
            .ToListAsync(ct);
        if (!showExpired) proCerts = proCerts.Where(x => !x.IsExpired).ToList();
        var participantRows = await db.CourseParticipants.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderByDescending(x => x.Course!.PlannedEndDate)
            .Select(x => new
            {
                ParticipantId = x.Id, x.CourseId, x.Course!.CourseNo,
                x.Course.TitleAr, x.Course.Status, x.AdminCertificateOverride,
                x.HistoricalPassed,
                CertificateReference = db.Certificates.IgnoreQueryFilters()
                    .Where(c => c.ParticipantId == x.Id && c.Status == CertificateStatus.Issued &&
                        c.IsActive && !c.HiddenByCourseDisable)
                    .Select(c => c.ReferenceNo).FirstOrDefault()
            }).ToListAsync(ct);
        var participations = new List<CourseParticipationDto>(participantRows.Count);
        foreach (var row in participantRows)
        {
            var state = await eligibility.EvaluateAsync(row.CourseId,
                row.ParticipantId, false, ct);
            if (!state.Ok) continue;
            bool final = row.Status == CourseStatus.Finished ||
                row.AdminCertificateOverride || row.HistoricalPassed.HasValue;
            participations.Add(new CourseParticipationDto(row.CourseId, row.CourseNo,
                row.TitleAr, row.Status, final, state.Value!.Eligible,
                row.CertificateReference, state.Value.UnmetReasons));
        }
        var profile = memberProfile;
        return Result<ProfileDto>.Success(new ProfileDto(user.Id, user.FullNameAr,
            user.FullNameEn, user.CenterName, user.DepartmentName, user.JobTitle,
            profile?.BioAr, profile?.BioEn, profile?.ExpertiseAr ?? [],
            profile?.ContactMethod, profile?.ContactValue, profile?.PhotoFileId,
            platformCerts, proCerts, proCerts.Count, participations));
    }

    public async Task<Result> UpdateOwnAsync(long actorUserId, string? bioAr,
        string? bioEn, string? contactMethod, string? contactValue,
        bool showInDirectory, CancellationToken ct)
    {
        if (string.Equals(contactMethod, "Email", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(contactValue) ||
             !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(contactValue)))
            return Result.Failure("CONTACT_EMAIL_INVALID");
        var profile = await db.MemberProfiles
            .SingleOrDefaultAsync(x => x.UserId == actorUserId, ct);
        if (profile is null)
        {
            profile = new MemberProfile { UserId = actorUserId, ShowInDirectory = false };
            db.MemberProfiles.Add(profile);
        }
        profile.BioAr = Clean(bioAr); profile.BioEn = Clean(bioEn);
        profile.ContactMethod = Clean(contactMethod);
        profile.ContactValue = Clean(contactValue);
        profile.ShowInDirectory = showInDirectory;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 1000)];
}

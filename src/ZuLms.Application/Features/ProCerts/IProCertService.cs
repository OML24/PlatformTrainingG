using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Files;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.ProCerts;

public interface IProCertService
{
    Task<Result<long>> SubmitAsync(long actorUserId, ProCertInput input,
        CancellationToken ct);
    /// <summary>Owner withdraws (logical disable) an approved certificate.</summary>
    Task<Result> WithdrawAsync(long actorUserId, long certId, CancellationToken ct);
    /// <summary>Quality/admin correction; the rejected owner edit resubmits instead.</summary>
    Task<Result> EditAsync(long actorUserId, long certId, ProCertInput input,
        CancellationToken ct);
    /// <summary>The rejected owner edits and resubmits (status back to Submitted).</summary>
    Task<Result> ResubmitAsync(long actorUserId, long certId, ProCertInput input,
        CancellationToken ct);
    Task<Result> DecideAsync(long actorUserId, long certId, bool approve,
        string? notes, CancellationToken ct);
    Task<Result> DeactivateAsync(long actorUserId, long certId, string reason,
        CancellationToken ct);
    Task<Result<ProCertPageDto>> ListAsync(long actorUserId, string? search,
        ProCertStatus? status, long? categoryId, bool includeInactive,
        int page, CancellationToken ct);
    Task<Result<ProCertDetailsDto>> GetAsync(long actorUserId, long certId,
        CancellationToken ct);
    Task<Result<FileDownloadDto>> GetFileAsync(long actorUserId, long certId,
        CancellationToken ct);
    Task<Result<IReadOnlyList<ProCertCategoryDto>>> CategoriesAsync(
        CancellationToken ct);
    Task<Result> SaveCategoryAsync(long actorUserId, long? categoryId,
        string nameAr, string? nameEn, int sortOrder, CancellationToken ct);
    Task<Result<ProCertStatsDto>> StatsAsync(long actorUserId, long? facultyId,
        long? categoryId, string? issuer, DateOnly? from, DateOnly? to,
        bool includeExpired, CancellationToken ct);
}

public sealed record ProCertInput(
    string Name, string? NameAr, string IssuerName, string? IssuerWebsiteUrl,
    long CategoryId, string? Description, string? CredentialCode, string? VerifyUrl,
    DateOnly IssuedDate, DateOnly? ExpiryDate, long FileId);

public sealed record ProCertRowDto(long Id, long UserId, string OwnerNameAr,
    string Name, string IssuerName, long CategoryId, string CategoryNameAr,
    ProCertStatus Status, DateOnly IssuedDate, DateOnly? ExpiryDate,
    bool IsExpired, string? CredentialCode, string? VerifyUrl, long FileId,
    string? DecisionNotes, DateTimeOffset SubmittedAt);

public sealed record ProCertPageDto(int Page, int PageSize, int Total,
    IReadOnlyList<ProCertRowDto> Items);

public sealed record ProCertDetailsDto(ProCertRowDto Certificate,
    IReadOnlyList<string> Events);

public sealed record ProCertCategoryDto(long Id, string NameAr, string? NameEn,
    int SortOrder);

public sealed record ProCertStatsTotalsDto(int Certs, int Members,
    double AvgPerMember, int MembersWithAtLeastOne, double CoveragePercent);

public sealed record ProCertTopMemberDto(long UserId, string Name,
    string FacultyName, int Count);

public sealed record ProCertFacultyTopDto(long CenterId, string FacultyName,
    IReadOnlyList<ProCertTopMemberDto> Members);

public sealed record ProCertFacultyDto(long CenterId, string FacultyName,
    int Certs, int Members, double AvgPerMember);

public sealed record ProCertCategoryStatDto(long CategoryId, string Name,
    int Certs, int Members);

public sealed record ProCertIssuerDto(string Issuer, int Certs, int Members);

public sealed record ProCertYearDto(int Year, int Certs);

public sealed record ProCertStatsDto(ProCertStatsTotalsDto Totals,
    IReadOnlyList<ProCertTopMemberDto> TopMembers,
    IReadOnlyList<ProCertFacultyTopDto> TopMembersByFaculty,
    IReadOnlyList<ProCertFacultyDto> ByFaculty,
    IReadOnlyList<ProCertCategoryStatDto> ByCategory,
    IReadOnlyList<ProCertIssuerDto> TopIssuers,
    IReadOnlyList<ProCertYearDto> ByYear);

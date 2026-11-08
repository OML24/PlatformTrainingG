using ZuLms.Application.Common.Results;
using ZuLms.Application.Features.Files;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Certificates;

public interface ICertificateService
{
    /// <summary>Issues a certificate atomically for an eligible participant. Requires the caller's transaction.</summary>
    Task<Result<CertificateIssueDto>> IssueAsync(long courseId, long participantId,
        long? actorUserId, CancellationToken ct);
    Task<Result> RevokeAsync(long actorUserId, long certificateId, string reason,
        CancellationToken ct);
    /// <summary>Issues certificates for every eligible participant without one (optionally for one course).</summary>
    Task<Result<int>> IssuePendingAsync(long actorUserId, long? courseId, CancellationToken ct);
    Task<Result<AdministrativeCertificateResultDto>> ForceIssueAsync(long actorUserId,
        long courseId, int employeeCode, bool evaluationExempt, CancellationToken ct);
    Task<Result<CertificatePageDto>> ListAsync(long actorUserId, string? search,
        long? courseId, CertificateStatus? status, int page, CancellationToken ct);
    Task<Result<IReadOnlyList<CertificateCardDto>>> MineAsync(long actorUserId,
        CancellationToken ct);
    Task<Result<FileDownloadDto>> GetPdfAsync(long actorUserId, long certificateId,
        CancellationToken ct);
    /// <summary>Public verification by reference number; null means "not found" (no details revealed).</summary>
    Task<Result<CertificateVerifyDto?>> VerifyAsync(string referenceNo, CancellationToken ct);
}

public interface ICertificateTemplateService
{
    Task<Result<CertificateTemplateDto>> GetCurrentAsync(long actorUserId, CancellationToken ct);
    Task<Result<CertificateTemplateDto>> UploadAsync(long actorUserId, string fileName,
        Stream content, long declaredLength, string layoutJson, CancellationToken ct);
    /// <summary>Renders a sample certificate with explicit dummy data.</summary>
    Task<Result<byte[]>> PreviewAsync(long actorUserId, CancellationToken ct);
}

public sealed record CertificateIssueDto(long Id, string ReferenceNo);
public sealed record AdministrativeCertificateResultDto(bool Issued,
    bool PendingEvaluation, long? CertificateId, string? ReferenceNo);

public sealed record CertificateRowDto(long Id, string ReferenceNo, long CourseId,
    string CourseNo, string CourseTitleAr, string HolderNameAr, CertificateStatus Status,
    DateTimeOffset IssuedAt, DateTimeOffset? RevokedAt, bool IsActive);

public sealed record CertificatePageDto(int Page, int PageSize, int Total,
    IReadOnlyList<CertificateRowDto> Items);

public sealed record CertificateCardDto(long Id, string ReferenceNo, long CourseId,
    string CourseNo, string CourseTitleAr, string CoursePeriodText, decimal CourseHours,
    string InstructorNameAr, DateTimeOffset IssuedAt, string VerifyUrl);

public sealed record CertificateVerifyDto(string ReferenceNo, string HolderNameAr,
    string? HolderNameEn, string CourseTitleAr, string? CourseTitleEn,
    string CoursePeriodText, decimal CourseHours, string InstructorNameAr,
    DateTimeOffset IssuedAt, DateTimeOffset? RevokedAt, bool Revoked);

public sealed record CertificateTemplateDto(long Id, int VersionNo, long FileId,
    decimal PageWidthPt, decimal PageHeightPt, string LayoutJson, bool IsCurrent,
    DateTimeOffset CreatedAt);

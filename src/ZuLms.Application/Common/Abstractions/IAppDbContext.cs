using Microsoft.EntityFrameworkCore;
using ZuLms.Domain.Entities.Audit;
using ZuLms.Domain.Entities.Certificates;
using ZuLms.Domain.Entities.Config;
using ZuLms.Domain.Entities.Evaluation;
using ZuLms.Domain.Entities.Files;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Entities.Needs;
using ZuLms.Domain.Entities.Notify;
using ZuLms.Domain.Entities.Org;
using ZuLms.Domain.Entities.Profiles;
using ZuLms.Domain.Entities.Sync;
using ZuLms.Domain.Entities.Training;

namespace ZuLms.Application.Common.Abstractions;

public interface IAppDbContext
{
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Certificate> Certificates { get; }
    DbSet<CertificateTemplate> CertificateTemplates { get; }
    DbSet<FieldHint> FieldHints { get; }
    DbSet<NumberSequence> NumberSequences { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<EvaluationAnswer> EvaluationAnswers { get; }
    DbSet<EvaluationFormVersion> EvaluationFormVersions { get; }
    DbSet<EvaluationOption> EvaluationOptions { get; }
    DbSet<EvaluationQuestion> EvaluationQuestions { get; }
    DbSet<EvaluationResponse> EvaluationResponses { get; }
    DbSet<StoredFile> StoredFiles { get; }
    DbSet<VaultDocument> VaultDocuments { get; }
    DbSet<BlockedPassword> BlockedPasswords { get; }
    DbSet<ExternalInstructorProfile> ExternalInstructorProfiles { get; }
    DbSet<ForgotPasswordAttempt> ForgotPasswordAttempts { get; }
    DbSet<PasswordPolicy> PasswordPolicys { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Role> Roles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<User> Users { get; }
    DbSet<UserPermissionOverride> UserPermissionOverrides { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<TrainingNeed> TrainingNeeds { get; }
    DbSet<TrainingNeedEvent> TrainingNeedEvents { get; }
    DbSet<EmailOutbox> EmailOutboxs { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Center> Centers { get; }
    DbSet<Department> Departments { get; }
    DbSet<Employee> Employees { get; }
    DbSet<UniversitySide> UniversitySides { get; }
    DbSet<MemberProfile> MemberProfiles { get; }
    DbSet<ProfessionalCertCategory> ProfessionalCertCategorys { get; }
    DbSet<ProfessionalCertEvent> ProfessionalCertEvents { get; }
    DbSet<ProfessionalCertification> ProfessionalCertifications { get; }
    DbSet<SyncReviewItem> SyncReviewItems { get; }
    DbSet<SyncRun> SyncRuns { get; }
    DbSet<AttendanceChange> AttendanceChanges { get; }
    DbSet<AttendanceCodeAttempt> AttendanceCodeAttempts { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<Course> Courses { get; }
    DbSet<CourseCancellationRequest> CourseCancellationRequests { get; }
    DbSet<CourseExam> CourseExams { get; }
    DbSet<CourseFile> CourseFiles { get; }
    DbSet<CourseParticipant> CourseParticipants { get; }
    DbSet<CourseProposalRevision> CourseProposalRevisions { get; }
    DbSet<CourseStatusHistory> CourseStatusHistorys { get; }
    DbSet<ExamResult> ExamResults { get; }
    DbSet<ExamResultHistory> ExamResultHistorys { get; }
    DbSet<ExternalInstructorAccountRequest> ExternalInstructorAccountRequests { get; }
    DbSet<ImpactSubmission> ImpactSubmissions { get; }
    DbSet<Lecture> Lectures { get; }
    DbSet<TrainingTrack> TrainingTracks { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken);
    Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken);
    Task InsertSharedEmailReviewIfAbsentAsync(string entityKey, string detailsJson,
        CancellationToken cancellationToken);
    Task<string> NextCourseNumberAsync(int year, CancellationToken cancellationToken);
    /// <summary>Atomically increments and returns the next value of a named sequence (requires an active transaction).</summary>
    Task<long> NextSequenceAsync(string sequenceKey, CancellationToken cancellationToken);
}


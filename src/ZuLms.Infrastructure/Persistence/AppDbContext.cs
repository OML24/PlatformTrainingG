using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ZuLms.Application.Common.Abstractions;
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

namespace ZuLms.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public const string Schema = "zulms_rebuild";
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<CertificateTemplate> CertificateTemplates => Set<CertificateTemplate>();
    public DbSet<FieldHint> FieldHints => Set<FieldHint>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<EvaluationAnswer> EvaluationAnswers => Set<EvaluationAnswer>();
    public DbSet<EvaluationFormVersion> EvaluationFormVersions => Set<EvaluationFormVersion>();
    public DbSet<EvaluationOption> EvaluationOptions => Set<EvaluationOption>();
    public DbSet<EvaluationQuestion> EvaluationQuestions => Set<EvaluationQuestion>();
    public DbSet<EvaluationResponse> EvaluationResponses => Set<EvaluationResponse>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<VaultDocument> VaultDocuments => Set<VaultDocument>();
    public DbSet<BlockedPassword> BlockedPasswords => Set<BlockedPassword>();
    public DbSet<ExternalInstructorProfile> ExternalInstructorProfiles => Set<ExternalInstructorProfile>();
    public DbSet<ForgotPasswordAttempt> ForgotPasswordAttempts => Set<ForgotPasswordAttempt>();
    public DbSet<PasswordPolicy> PasswordPolicys => Set<PasswordPolicy>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<TrainingNeed> TrainingNeeds => Set<TrainingNeed>();
    public DbSet<TrainingNeedEvent> TrainingNeedEvents => Set<TrainingNeedEvent>();
    public DbSet<EmailOutbox> EmailOutboxs => Set<EmailOutbox>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Center> Centers => Set<Center>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<UniversitySide> UniversitySides => Set<UniversitySide>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<ProfessionalCertCategory> ProfessionalCertCategorys => Set<ProfessionalCertCategory>();
    public DbSet<ProfessionalCertEvent> ProfessionalCertEvents => Set<ProfessionalCertEvent>();
    public DbSet<ProfessionalCertification> ProfessionalCertifications => Set<ProfessionalCertification>();
    public DbSet<SyncReviewItem> SyncReviewItems => Set<SyncReviewItem>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<AttendanceChange> AttendanceChanges => Set<AttendanceChange>();
    public DbSet<AttendanceCodeAttempt> AttendanceCodeAttempts => Set<AttendanceCodeAttempt>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseCancellationRequest> CourseCancellationRequests => Set<CourseCancellationRequest>();
    public DbSet<CourseExam> CourseExams => Set<CourseExam>();
    public DbSet<CourseFile> CourseFiles => Set<CourseFile>();
    public DbSet<CourseParticipant> CourseParticipants => Set<CourseParticipant>();
    public DbSet<CourseProposalRevision> CourseProposalRevisions => Set<CourseProposalRevision>();
    public DbSet<CourseStatusHistory> CourseStatusHistorys => Set<CourseStatusHistory>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<ExamResultHistory> ExamResultHistorys => Set<ExamResultHistory>();
    public DbSet<ExternalInstructorAccountRequest> ExternalInstructorAccountRequests => Set<ExternalInstructorAccountRequest>();
    public DbSet<ImpactSubmission> ImpactSubmissions => Set<ImpactSubmission>();
    public DbSet<Lecture> Lectures => Set<Lecture>();
    public DbSet<TrainingTrack> TrainingTracks => Set<TrainingTrack>();

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is not null) return await action();
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is { } existing)
        {
            if (existing.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.Serializable)
                throw new InvalidOperationException("An existing transaction must be serializable.");
            return await action();
        }
        await using var transaction = await Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task InsertSharedEmailReviewIfAbsentAsync(string entityKey, string detailsJson,
        CancellationToken cancellationToken) => Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO zulms_rebuild.sync_review_items
                (entity_type, entity_key, reason, details, first_seen_run_id, origin)
            VALUES ('Email', {entityKey}, 'SharedEmail', {detailsJson}, NULL, 'ForgotPassword')
            ON CONFLICT DO NOTHING
            """, cancellationToken);

    public async Task<string> NextCourseNumberAsync(int year, CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is null)
            throw new InvalidOperationException("Course numbering requires an active transaction.");
        var connection = Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = Database.CurrentTransaction.GetDbTransaction();
        command.CommandText = """
            INSERT INTO zulms_rebuild.number_sequences (sequence_key, last_value)
            VALUES (@key, 1)
            ON CONFLICT (sequence_key) DO UPDATE
            SET last_value = number_sequences.last_value + 1
            RETURNING last_value
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = $"course:{year}";
        command.Parameters.Add(parameter);
        long sequence = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        return $"C-{year:D4}-{sequence:D4}";
    }

    public async Task<long> NextSequenceAsync(string sequenceKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sequenceKey);
        if (Database.CurrentTransaction is null)
            throw new InvalidOperationException("Sequence numbering requires an active transaction.");
        var connection = Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = Database.CurrentTransaction.GetDbTransaction();
        command.CommandText = """
            INSERT INTO zulms_rebuild.number_sequences (sequence_key, last_value)
            VALUES (@key, 1)
            ON CONFLICT (sequence_key) DO UPDATE
            SET last_value = number_sequences.last_value + 1
            RETURNING last_value
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = sequenceKey;
        command.Parameters.Add(parameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}


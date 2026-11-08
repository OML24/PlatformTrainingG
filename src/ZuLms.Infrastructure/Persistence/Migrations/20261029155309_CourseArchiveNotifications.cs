using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZuLms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CourseArchiveNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                schema: "zulms_rebuild",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                schema: "zulms_rebuild",
                table: "notifications",
                sql: "type IN ('ProposalSubmitted', 'ProposalDecision', 'CourseApproved', 'ParticipantAdded', 'ParticipantRemoved', 'LectureCreated', 'CourseFinished', 'CertificateIssued', 'CertificateRevoked', 'EvaluationReopened', 'ImpactSubmitted', 'ImpactDecision', 'CancellationRequested', 'CancellationDecision', 'CourseCancelled', 'CourseDisabled', 'CourseRestored', 'CourseArchived', 'CourseUnarchived', 'NeedSubmitted', 'NeedDecision', 'ExtInstructorRequested', 'ExtInstructorReady', 'ProCertSubmitted', 'ProCertApproved', 'ProCertRejected', 'SyncReviewItems')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                schema: "zulms_rebuild",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                schema: "zulms_rebuild",
                table: "notifications",
                sql: "type IN ('ProposalSubmitted', 'ProposalDecision', 'CourseApproved', 'ParticipantAdded', 'ParticipantRemoved', 'LectureCreated', 'CourseFinished', 'CertificateIssued', 'CertificateRevoked', 'EvaluationReopened', 'ImpactSubmitted', 'ImpactDecision', 'CancellationRequested', 'CancellationDecision', 'CourseCancelled', 'CourseDisabled', 'CourseRestored', 'NeedSubmitted', 'NeedDecision', 'ExtInstructorRequested', 'ExtInstructorReady', 'ProCertSubmitted', 'ProCertApproved', 'ProCertRejected', 'SyncReviewItems')");
        }
    }
}

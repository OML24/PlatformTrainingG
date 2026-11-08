using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZuLms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixManagedInstructorAndPasswordStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_instructor_kind",
                schema: "zulms_rebuild",
                table: "courses");

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_instructor_kind",
                schema: "zulms_rebuild",
                table: "courses",
                sql: "instructor_kind IN ('Internal', 'ExternalAccount', 'ExternalNameOnly', 'ManagedByQuality')");

            migrationBuilder.Sql("""
                UPDATE zulms_rebuild.courses
                SET instructor_kind = 'ManagedByQuality', instructor_name_ar = 'تديرها الجودة'
                WHERE instructor_kind = 'ExternalNameOnly' AND managed_by_quality
                  AND (instructor_name_ar IS NULL OR btrim(instructor_name_ar) = '');
                UPDATE zulms_rebuild.users
                SET password_enc = '', initial_password_enc = NULL
                WHERE first_login_completed_at IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE zulms_rebuild.courses
                SET instructor_kind = 'ExternalNameOnly'
                WHERE instructor_kind = 'ManagedByQuality';
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_instructor_kind",
                schema: "zulms_rebuild",
                table: "courses");

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_instructor_kind",
                schema: "zulms_rebuild",
                table: "courses",
                sql: "instructor_kind IN ('Internal', 'ExternalAccount', 'ExternalNameOnly')");
        }
    }
}


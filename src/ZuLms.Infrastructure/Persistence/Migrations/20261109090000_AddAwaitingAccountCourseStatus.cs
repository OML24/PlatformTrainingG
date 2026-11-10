using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ZuLms.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261109090000_AddAwaitingAccountCourseStatus")]
public sealed class AddAwaitingAccountCourseStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE zulms_rebuild.courses DROP CONSTRAINT ck_courses_status;");
        migrationBuilder.Sql("ALTER TABLE zulms_rebuild.course_status_history DROP CONSTRAINT ck_course_status_history_from_status;");
        migrationBuilder.Sql("ALTER TABLE zulms_rebuild.course_status_history DROP CONSTRAINT ck_course_status_history_to_status;");
        migrationBuilder.Sql("ALTER TABLE zulms_rebuild.courses ADD CONSTRAINT ck_courses_status CHECK (status IN ('Proposed', 'Returned', 'Rejected', 'Approved', 'Finished', 'Cancelled', 'AwaitingAccount'));");
        migrationBuilder.Sql("ALTER TABLE zulms_rebuild.course_status_history ADD CONSTRAINT ck_course_status_history_from_status CHECK (from_status IS NULL OR from_status IN ('Proposed', 'Returned', 'Rejected', 'Approved', 'Finished', 'Cancelled', 'AwaitingAccount'));");
        migrationBuilder.Sql("ALTER TABLE zulms_rebuild.course_status_history ADD CONSTRAINT ck_course_status_history_to_status CHECK (to_status IN ('Proposed', 'Returned', 'Rejected', 'Approved', 'Finished', 'Cancelled', 'AwaitingAccount'));");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE zulms_rebuild.courses SET status = 'Approved' WHERE status = 'AwaitingAccount';");
        migrationBuilder.Sql("UPDATE zulms_rebuild.course_status_history SET from_status = 'Approved' WHERE from_status = 'AwaitingAccount';");
        migrationBuilder.Sql("UPDATE zulms_rebuild.course_status_history SET to_status = 'Approved' WHERE to_status = 'AwaitingAccount';");
        migrationBuilder.Sql("ALTER TABLE zulms_rebuild.courses DROP CONSTRAINT ck_courses_status;");
        migrationBuilder.Sql("ALTER TABLE zulms_rebuild.course_status_history DROP CONSTRAINT ck_course_status_history_from_status;");
        migrationBuilder.Sql("ALTER TABLE zulms_rebuild.course_status_history DROP CONSTRAINT ck_course_status_history_to_status;");
        migrationBuilder.Sql("ALTER TABLE zulms_rebuild.courses ADD CONSTRAINT ck_courses_status CHECK (status IN ('Proposed', 'Returned', 'Rejected', 'Approved', 'Finished', 'Cancelled'));");
        migrationBuilder.Sql("ALTER TABLE zulms_rebuild.course_status_history ADD CONSTRAINT ck_course_status_history_from_status CHECK (from_status IS NULL OR from_status IN ('Proposed', 'Returned', 'Rejected', 'Approved', 'Finished', 'Cancelled'));");
        migrationBuilder.Sql("ALTER TABLE zulms_rebuild.course_status_history ADD CONSTRAINT ck_course_status_history_to_status CHECK (to_status IN ('Proposed', 'Returned', 'Rejected', 'Approved', 'Finished', 'Cancelled'));");
    }
}

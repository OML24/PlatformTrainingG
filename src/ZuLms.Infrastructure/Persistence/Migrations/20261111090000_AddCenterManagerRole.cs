using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZuLms.Infrastructure.Persistence.Migrations
{
    /// <summary>Allows the new CenterManager role code (center/department manager).</summary>
    public partial class AddCenterManagerRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_roles_code",
                schema: "zulms_rebuild",
                table: "roles");

            migrationBuilder.AddCheckConstraint(
                name: "ck_roles_code",
                schema: "zulms_rebuild",
                table: "roles",
                sql: "code IN ('Admin', 'Quality', 'Dean', 'Instructor', 'Member', 'ExternalInstructor', 'CenterManager')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM zulms_rebuild.role_permissions WHERE role_id IN (SELECT id FROM zulms_rebuild.roles WHERE code = 'CenterManager');");
            migrationBuilder.Sql(
                "UPDATE zulms_rebuild.user_roles SET is_active = false WHERE role_id IN (SELECT id FROM zulms_rebuild.roles WHERE code = 'CenterManager');");

            migrationBuilder.DropCheckConstraint(
                name: "ck_roles_code",
                schema: "zulms_rebuild",
                table: "roles");

            migrationBuilder.AddCheckConstraint(
                name: "ck_roles_code",
                schema: "zulms_rebuild",
                table: "roles",
                sql: "code IN ('Admin', 'Quality', 'Dean', 'Instructor', 'Member', 'ExternalInstructor')");
        }
    }
}

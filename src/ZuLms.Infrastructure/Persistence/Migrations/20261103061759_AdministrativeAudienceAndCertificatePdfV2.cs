using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZuLms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdministrativeAudienceAndCertificatePdfV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_audience_type",
                schema: "zulms_rebuild",
                table: "courses");

            migrationBuilder.AddColumn<int>(
                name: "pdf_render_version",
                schema: "zulms_rebuild",
                table: "certificates",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_audience_type",
                schema: "zulms_rebuild",
                table: "courses",
                sql: "audience_type IN ('General', 'Faculty', 'Administrative', 'Named')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_audience_type",
                schema: "zulms_rebuild",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "pdf_render_version",
                schema: "zulms_rebuild",
                table: "certificates");

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_audience_type",
                schema: "zulms_rebuild",
                table: "courses",
                sql: "audience_type IN ('General', 'Faculty', 'Named')");
        }
    }
}

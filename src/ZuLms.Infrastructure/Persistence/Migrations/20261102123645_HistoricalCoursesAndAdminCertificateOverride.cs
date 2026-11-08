using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZuLms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HistoricalCoursesAndAdminCertificateOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "impact_end_date",
                schema: "zulms_rebuild",
                table: "courses",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "admin_certificate_override",
                schema: "zulms_rebuild",
                table: "course_participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "evaluation_exempt",
                schema: "zulms_rebuild",
                table: "course_participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "historical_passed",
                schema: "zulms_rebuild",
                table: "course_participants",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "impact_end_date",
                schema: "zulms_rebuild",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "admin_certificate_override",
                schema: "zulms_rebuild",
                table: "course_participants");

            migrationBuilder.DropColumn(
                name: "evaluation_exempt",
                schema: "zulms_rebuild",
                table: "course_participants");

            migrationBuilder.DropColumn(
                name: "historical_passed",
                schema: "zulms_rebuild",
                table: "course_participants");
        }
    }
}

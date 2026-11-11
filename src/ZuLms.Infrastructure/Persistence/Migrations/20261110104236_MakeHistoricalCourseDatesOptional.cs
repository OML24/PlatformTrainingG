using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZuLms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeHistoricalCourseDatesOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "planned_start_date",
                schema: "zulms_rebuild",
                table: "courses",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "planned_end_date",
                schema: "zulms_rebuild",
                table: "courses",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "planned_start_date",
                schema: "zulms_rebuild",
                table: "courses",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "planned_end_date",
                schema: "zulms_rebuild",
                table: "courses",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}

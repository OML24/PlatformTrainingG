using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZuLms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncReviewOriginAndOptionalRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "first_seen_run_id",
                schema: "zulms_rebuild",
                table: "sync_review_items",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "origin",
                schema: "zulms_rebuild",
                table: "sync_review_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Sync");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sync_review_items_origin",
                schema: "zulms_rebuild",
                table: "sync_review_items",
                sql: "origin IN ('Sync', 'ForgotPassword', 'Provisioning')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sync_review_items_origin",
                schema: "zulms_rebuild",
                table: "sync_review_items");

            migrationBuilder.DropColumn(
                name: "origin",
                schema: "zulms_rebuild",
                table: "sync_review_items");

            migrationBuilder.AlterColumn<long>(
                name: "first_seen_run_id",
                schema: "zulms_rebuild",
                table: "sync_review_items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}

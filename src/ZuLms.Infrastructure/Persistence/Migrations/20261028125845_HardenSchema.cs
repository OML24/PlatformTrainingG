using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZuLms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "security_stamp",
                schema: "zulms_rebuild",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "current_revision",
                schema: "zulms_rebuild",
                table: "training_needs",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string[]>(
                name: "expertise_en",
                schema: "zulms_rebuild",
                table: "member_profiles",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]",
                oldClrType: typeof(string[]),
                oldType: "text[]");

            migrationBuilder.AlterColumn<string[]>(
                name: "expertise_ar",
                schema: "zulms_rebuild",
                table: "member_profiles",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]",
                oldClrType: typeof(string[]),
                oldType: "text[]");

            migrationBuilder.AlterColumn<int>(
                name: "current_revision_no",
                schema: "zulms_rebuild",
                table: "courses",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ux_user_roles_active_scope
                ON zulms_rebuild.user_roles (user_id, role_id, COALESCE(scope_center_id, 0))
                WHERE is_active;
                """);
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ux_procert_active_credential
                ON zulms_rebuild.professional_certifications
                (user_id, lower(issuer_name), lower(credential_code))
                WHERE credential_code IS NOT NULL AND is_active AND status <> 'Rejected';
                """);
            migrationBuilder.Sql("""
                CREATE FUNCTION zulms_rebuild.reject_audit_mutation() RETURNS trigger
                LANGUAGE plpgsql AS $$ BEGIN
                  RAISE EXCEPTION 'audit_logs is append only';
                END $$;
                CREATE TRIGGER audit_logs_no_update_delete
                BEFORE UPDATE OR DELETE ON zulms_rebuild.audit_logs
                FOR EACH ROW EXECUTE FUNCTION zulms_rebuild.reject_audit_mutation();
                CREATE TRIGGER audit_logs_no_truncate
                BEFORE TRUNCATE ON zulms_rebuild.audit_logs
                FOR EACH STATEMENT EXECUTE FUNCTION zulms_rebuild.reject_audit_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER audit_logs_no_truncate ON zulms_rebuild.audit_logs;");
            migrationBuilder.Sql("DROP TRIGGER audit_logs_no_update_delete ON zulms_rebuild.audit_logs;");
            migrationBuilder.Sql("DROP FUNCTION zulms_rebuild.reject_audit_mutation();");
            migrationBuilder.Sql("DROP INDEX zulms_rebuild.ux_procert_active_credential;");
            migrationBuilder.Sql("DROP INDEX zulms_rebuild.ux_user_roles_active_scope;");
            migrationBuilder.AlterColumn<Guid>(
                name: "security_stamp",
                schema: "zulms_rebuild",
                table: "users",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AlterColumn<int>(
                name: "current_revision",
                schema: "zulms_rebuild",
                table: "training_needs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<string[]>(
                name: "expertise_en",
                schema: "zulms_rebuild",
                table: "member_profiles",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(string[]),
                oldType: "text[]",
                oldDefaultValueSql: "'{}'::text[]");

            migrationBuilder.AlterColumn<string[]>(
                name: "expertise_ar",
                schema: "zulms_rebuild",
                table: "member_profiles",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(string[]),
                oldType: "text[]",
                oldDefaultValueSql: "'{}'::text[]");

            migrationBuilder.AlterColumn<int>(
                name: "current_revision_no",
                schema: "zulms_rebuild",
                table: "courses",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);
        }
    }
}

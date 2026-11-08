using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZuLms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "zulms_rebuild");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "number_sequences",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    sequence_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_number_sequences", x => x.sequence_key);
                });

            migrationBuilder.CreateTable(
                name: "password_policy",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    min_length = table.Column<int>(type: "integer", nullable: false),
                    max_length = table.Column<int>(type: "integer", nullable: false),
                    allow_spaces = table.Column<bool>(type: "boolean", nullable: false),
                    allow_arabic = table.Column<bool>(type: "boolean", nullable: false),
                    block_common = table.Column<bool>(type: "boolean", nullable: false),
                    block_context = table.Column<bool>(type: "boolean", nullable: false),
                    require_upper = table.Column<bool>(type: "boolean", nullable: false),
                    require_lower = table.Column<bool>(type: "boolean", nullable: false),
                    require_digit = table.Column<bool>(type: "boolean", nullable: false),
                    require_symbol = table.Column<bool>(type: "boolean", nullable: false),
                    rotation_days = table.Column<int>(type: "integer", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_policy", x => x.id);
                    table.CheckConstraint("ck_password_policy_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    perm_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    group_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.id);
                    table.CheckConstraint("ck_permissions_kind", "kind IN ('PageView', 'Action')");
                    table.CheckConstraint("ck_permissions_scope", "scope IN ('Global', 'OwnFaculty', 'InstructedCourse', 'Self')");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name_ar = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_assignable = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                    table.CheckConstraint("ck_roles_code", "code IN ('Admin', 'Quality', 'Dean', 'Instructor', 'Member', 'ExternalInstructor')");
                });

            migrationBuilder.CreateTable(
                name: "university_sides",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    side_code = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_imported = table.Column<bool>(type: "boolean", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_university_sides", x => x.side_code);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    permission_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_changes",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    lecture_id = table.Column<long>(type: "bigint", nullable: false),
                    participant_id = table.Column<long>(type: "bigint", nullable: false),
                    old_present = table.Column<bool>(type: "boolean", nullable: true),
                    new_present = table.Column<bool>(type: "boolean", nullable: false),
                    source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    changed_by = table.Column<long>(type: "bigint", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_changes", x => x.id);
                    table.CheckConstraint("ck_attendance_changes_source", "source IN ('Code', 'Manual')");
                });

            migrationBuilder.CreateTable(
                name: "attendance_code_attempts",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    lecture_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    wrong_count = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_code_attempts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attendance_records",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    lecture_id = table.Column<long>(type: "bigint", nullable: false),
                    participant_id = table.Column<long>(type: "bigint", nullable: false),
                    is_present = table.Column<bool>(type: "boolean", nullable: false),
                    source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    marked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    marked_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_records", x => x.id);
                    table.CheckConstraint("ck_attendance_records_source", "source IN ('Code', 'Manual')");
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<long>(type: "bigint", nullable: true),
                    actor_login = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    actor_name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    actor_roles = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    device_summary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    action_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    entity_id = table.Column<long>(type: "bigint", nullable: true),
                    entity_label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    course_id = table.Column<long>(type: "bigint", nullable: true),
                    summary_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    summary_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.CheckConstraint("ck_audit_logs_category", "category IN ('Auth', 'User', 'Permission', 'Settings', 'Course', 'Lecture', 'Attendance', 'Exam', 'Impact', 'Evaluation', 'Certificate', 'Document', 'Sync', 'Need', 'System', 'Security')");
                });

            migrationBuilder.CreateTable(
                name: "blocked_passwords",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    password_normalized = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blocked_passwords", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "centers",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    side_code = table.Column<int>(type: "integer", nullable: false),
                    center_code = table.Column<int>(type: "integer", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    name_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    unit_code = table.Column<int>(type: "integer", nullable: true),
                    head_employee_code = table.Column<int>(type: "integer", nullable: true),
                    registrar_employee_code = table.Column<int>(type: "integer", nullable: true),
                    is_faculty = table.Column<bool>(type: "boolean", nullable: false),
                    short_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    source_missing = table.Column<bool>(type: "boolean", nullable: false),
                    source_missing_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    source_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_centers", x => x.id);
                    table.ForeignKey(
                        name: "fk_centers_university_sides_side_code",
                        column: x => x.side_code,
                        principalSchema: "zulms_rebuild",
                        principalTable: "university_sides",
                        principalColumn: "side_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "certificate_templates",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<long>(type: "bigint", nullable: false),
                    page_width_pt = table.Column<decimal>(type: "numeric", nullable: false),
                    page_height_pt = table.Column<decimal>(type: "numeric", nullable: false),
                    layout_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_certificate_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "certificates",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    reference_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    verification_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    participant_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    template_id = table.Column<long>(type: "bigint", nullable: false),
                    pdf_file_id = table.Column<long>(type: "bigint", nullable: false),
                    holder_name_ar = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    holder_name_en = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    course_title_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    course_title_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    course_period_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    course_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    instructor_name_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    instructor_name_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    revoked_by = table.Column<long>(type: "bigint", nullable: true),
                    revoked_note = table.Column<string>(type: "text", nullable: true),
                    hidden_by_course_disable = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_certificates", x => x.id);
                    table.CheckConstraint("ck_certificates_revoked_reason", "revoked_reason IN ('AttendanceCorrection', 'ExamResultChanged', 'ExamRequirementChanged', 'CourseCancelled', 'CourseReopened', 'ParticipantRemoved', 'CourseDisabledRestoreIneligible', 'ManualByAdmin')");
                    table.CheckConstraint("ck_certificates_status", "status IN ('Issued', 'Revoked')");
                    table.ForeignKey(
                        name: "fk_certificates_certificate_templates_template_id",
                        column: x => x.template_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "certificate_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_cancellation_requests",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    requested_by = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    letter_file_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decided_by = table.Column<long>(type: "bigint", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_cancellation_requests", x => x.id);
                    table.CheckConstraint("ck_course_cancellation_requests_status", "status IN ('Pending', 'Approved', 'Rejected')");
                });

            migrationBuilder.CreateTable(
                name: "course_exams",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    name_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_exams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "course_files",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    file_id = table.Column<long>(type: "bigint", nullable: false),
                    purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    lecture_id = table.Column<long>(type: "bigint", nullable: true),
                    revision_no = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_files", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "course_participants",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    added_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by = table.Column<long>(type: "bigint", nullable: true),
                    removal_reason = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_participants", x => x.id);
                    table.CheckConstraint("ck_course_participants_source", "source IN ('SelfRegistered', 'AddedByQuality', 'AddedByAdmin', 'HistoricalEntry')");
                });

            migrationBuilder.CreateTable(
                name: "course_proposal_revisions",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_by = table.Column<long>(type: "bigint", nullable: false),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decided_by = table.Column<long>(type: "bigint", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_proposal_revisions", x => x.id);
                    table.CheckConstraint("ck_course_proposal_revisions_decision", "decision IN ('Pending', 'Returned', 'Approved', 'Rejected')");
                });

            migrationBuilder.CreateTable(
                name: "course_status_history",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    changed_by = table.Column<long>(type: "bigint", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_status_history", x => x.id);
                    table.CheckConstraint("ck_course_status_history_from_status", "from_status IN ('Proposed', 'Returned', 'Rejected', 'Approved', 'Finished', 'Cancelled')");
                    table.CheckConstraint("ck_course_status_history_to_status", "to_status IN ('Proposed', 'Returned', 'Rejected', 'Approved', 'Finished', 'Cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "courses",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    course_no = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    track_id = table.Column<long>(type: "bigint", nullable: false),
                    delivery_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    objectives_ar = table.Column<string>(type: "text", nullable: false),
                    objectives_en = table.Column<string>(type: "text", nullable: true),
                    target_audience_ar = table.Column<string>(type: "text", nullable: false),
                    target_audience_en = table.Column<string>(type: "text", nullable: true),
                    total_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    planned_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    proposing_center_id = table.Column<long>(type: "bigint", nullable: true),
                    proposed_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    audience_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_center_id = table.Column<long>(type: "bigint", nullable: true),
                    registration_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    max_participants = table.Column<int>(type: "integer", nullable: true),
                    has_exam = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_by = table.Column<long>(type: "bigint", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    evaluation_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    evaluation_form_version_id = table.Column<long>(type: "bigint", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_by = table.Column<long>(type: "bigint", nullable: true),
                    final_lectures_count = table.Column<int>(type: "integer", nullable: true),
                    attendance_threshold_percent_override = table.Column<short>(type: "smallint", nullable: true),
                    managed_by_quality = table.Column<bool>(type: "boolean", nullable: false),
                    instructor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    instructor_user_id = table.Column<long>(type: "bigint", nullable: true),
                    instructor_name_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    instructor_name_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    instructor_org_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    instructor_org_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<long>(type: "bigint", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    is_historical = table.Column<bool>(type: "boolean", nullable: false),
                    historical_notes = table.Column<string>(type: "text", nullable: true),
                    current_revision_no = table.Column<int>(type: "integer", nullable: false),
                    last_return_notes = table.Column<string>(type: "text", nullable: true),
                    deactivation_certificates_action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    search_text = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courses", x => x.id);
                    table.CheckConstraint("ck_courses_attendance_override", "attendance_threshold_percent_override IS NULL OR attendance_threshold_percent_override BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_courses_audience_type", "audience_type IN ('General', 'Faculty', 'Named')");
                    table.CheckConstraint("ck_courses_dean_origin", "origin <> 'DeanProposal' OR proposing_center_id IS NOT NULL");
                    table.CheckConstraint("ck_courses_delivery_mode", "delivery_mode IN ('InPerson', 'Online', 'Blended')");
                    table.CheckConstraint("ck_courses_external_managed", "instructor_kind <> 'ExternalNameOnly' OR managed_by_quality");
                    table.CheckConstraint("ck_courses_faculty_target", "audience_type <> 'Faculty' OR target_center_id IS NOT NULL");
                    table.CheckConstraint("ck_courses_hours_positive", "total_hours > 0");
                    table.CheckConstraint("ck_courses_instructor_kind", "instructor_kind IN ('Internal', 'ExternalAccount', 'ExternalNameOnly')");
                    table.CheckConstraint("ck_courses_instructor_ref", "(instructor_kind IN ('Internal', 'ExternalAccount')) = (instructor_user_id IS NOT NULL)");
                    table.CheckConstraint("ck_courses_max_participants", "max_participants IS NULL OR max_participants > 0");
                    table.CheckConstraint("ck_courses_origin", "origin IN ('DeanProposal', 'QualityCreated', 'AdminCreated', 'Historical')");
                    table.CheckConstraint("ck_courses_planned_dates", "planned_end_date >= planned_start_date");
                    table.CheckConstraint("ck_courses_status", "status IN ('Proposed', 'Returned', 'Rejected', 'Approved', 'Finished', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_courses_centers_proposing_center_id",
                        column: x => x.proposing_center_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_courses_centers_target_center_id",
                        column: x => x.target_center_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    side_code = table.Column<int>(type: "integer", nullable: false),
                    department_code = table.Column<int>(type: "integer", nullable: false),
                    center_code = table.Column<int>(type: "integer", nullable: true),
                    center_id = table.Column<long>(type: "bigint", nullable: true),
                    name_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    name_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    reg_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    head_employee_code = table.Column<int>(type: "integer", nullable: true),
                    source_missing = table.Column<bool>(type: "boolean", nullable: false),
                    source_missing_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    source_hash = table.Column<string>(type: "text", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_departments", x => x.id);
                    table.ForeignKey(
                        name: "fk_departments_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_departments_university_sides_side_code",
                        column: x => x.side_code,
                        principalSchema: "zulms_rebuild",
                        principalTable: "university_sides",
                        principalColumn: "side_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "email_outbox",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    to_address = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    to_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: false),
                    related_user_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_outbox", x => x.id);
                    table.CheckConstraint("ck_email_outbox_kind", "kind IN ('InitialCredentials', 'ForgotPassword', 'ExternalInstructorAccount')");
                    table.CheckConstraint("ck_email_outbox_status", "status IN ('Pending', 'Sent', 'Failed', 'Uncertain')");
                });

            migrationBuilder.CreateTable(
                name: "employees",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    side_code = table.Column<int>(type: "integer", nullable: false),
                    employee_code = table.Column<int>(type: "integer", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    second_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    third_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    full_name_ar = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    full_name_en = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    center_code = table.Column<int>(type: "integer", nullable: true),
                    department_code = table.Column<int>(type: "integer", nullable: true),
                    center_id = table.Column<long>(type: "bigint", nullable: true),
                    department_id = table.Column<long>(type: "bigint", nullable: true),
                    main_job_code = table.Column<int>(type: "integer", nullable: true),
                    main_job_desc = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sub_job_code = table.Column<int>(type: "integer", nullable: true),
                    sub_job_desc = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    emp_status = table.Column<int>(type: "integer", nullable: true),
                    emp_status_desc = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    chk_emp_stat = table.Column<int>(type: "integer", nullable: true),
                    start_work_date = table.Column<DateOnly>(type: "date", nullable: true),
                    stop_work_date = table.Column<DateOnly>(type: "date", nullable: true),
                    source_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    is_academic = table.Column<bool>(type: "boolean", nullable: false),
                    search_text = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    source_missing = table.Column<bool>(type: "boolean", nullable: false),
                    source_missing_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    source_hash = table.Column<string>(type: "text", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employees", x => x.id);
                    table.ForeignKey(
                        name: "fk_employees_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employees_departments_department_id",
                        column: x => x.department_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employees_university_sides_side_code",
                        column: x => x.side_code,
                        principalSchema: "zulms_rebuild",
                        principalTable: "university_sides",
                        principalColumn: "side_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    account_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    login_name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    employee_id = table.Column<long>(type: "bigint", nullable: true),
                    full_name_ar = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    full_name_en = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    email_override = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    password_enc = table.Column<string>(type: "text", nullable: false),
                    initial_password_enc = table.Column<string>(type: "text", nullable: true),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    first_login_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    password_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    password_changed_by = table.Column<long>(type: "bigint", nullable: true),
                    security_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    credentials_email_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    credentials_email_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    credentials_email_last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    credentials_email_attempts = table.Column<int>(type: "integer", nullable: false),
                    access_override_allow = table.Column<bool>(type: "boolean", nullable: false),
                    access_override_by = table.Column<long>(type: "bigint", nullable: true),
                    access_override_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    access_override_reason = table.Column<string>(type: "text", nullable: true),
                    preferred_language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ui_settings = table.Column<string>(type: "jsonb", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    search_text = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_account_type", "account_type IN ('Employee', 'ExternalInstructor', 'Local')");
                    table.CheckConstraint("ck_users_credentials_email_status", "credentials_email_status IN ('NotSent', 'Sent', 'Failed', 'Uncertain')");
                    table.CheckConstraint("ck_users_employee_login", "account_type <> 'Employee' OR login_name ~ '^[0-9]+$'");
                    table.CheckConstraint("ck_users_employee_reference", "(account_type = 'Employee') = (employee_id IS NOT NULL)");
                    table.CheckConstraint("ck_users_external_login", "account_type <> 'ExternalInstructor' OR login_name ~ '^[0-9]{4}$'");
                    table.CheckConstraint("ck_users_preferred_language", "preferred_language IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_users_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_users_access_override_by",
                        column: x => x.access_override_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_users_password_changed_by",
                        column: x => x.password_changed_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_users_updated_by",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_form_versions",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_form_versions", x => x.id);
                    table.CheckConstraint("ck_evaluation_form_versions_status", "status IN ('Draft', 'Published')");
                    table.ForeignKey(
                        name: "fk_evaluation_form_versions_users_created_by_user_id",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evaluation_form_versions_users_published_by",
                        column: x => x.published_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evaluation_form_versions_users_updated_by_user_id",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_results",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    exam_id = table.Column<long>(type: "bigint", nullable: false),
                    participant_id = table.Column<long>(type: "bigint", nullable: false),
                    outcome = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    recorded_by = table.Column<long>(type: "bigint", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_results", x => x.id);
                    table.CheckConstraint("ck_exam_results_outcome", "outcome IN ('Passed', 'Failed')");
                    table.ForeignKey(
                        name: "fk_exam_results_course_exams_exam_id",
                        column: x => x.exam_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "course_exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_results_course_participants_participant_id",
                        column: x => x.participant_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "course_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_results_users_created_by_user_id",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_results_users_deactivated_by",
                        column: x => x.deactivated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_results_users_recorded_by",
                        column: x => x.recorded_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_results_users_updated_by_user_id",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_instructor_account_requests",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    requested_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    full_name_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    full_name_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    organization_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    organization_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    forwarded_by = table.Column<long>(type: "bigint", nullable: true),
                    forwarded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    handled_by = table.Column<long>(type: "bigint", nullable: true),
                    handled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_user_id = table.Column<long>(type: "bigint", nullable: true),
                    reject_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_instructor_account_requests", x => x.id);
                    table.CheckConstraint("ck_external_instructor_account_requests_status", "status IN ('PendingAdmin', 'Completed', 'Rejected')");
                    table.ForeignKey(
                        name: "fk_external_instructor_account_requests_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_instructor_account_requests_users_created_by_user_",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_instructor_account_requests_users_created_user_id",
                        column: x => x.created_user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_instructor_account_requests_users_forwarded_by",
                        column: x => x.forwarded_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_instructor_account_requests_users_handled_by",
                        column: x => x.handled_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_instructor_account_requests_users_requested_by_use",
                        column: x => x.requested_by_user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_instructor_account_requests_users_updated_by_user_",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "field_hints",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    field_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    context_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    hint_ar = table.Column<string>(type: "text", nullable: false),
                    hint_en = table.Column<string>(type: "text", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_hints", x => x.id);
                    table.ForeignKey(
                        name: "fk_field_hints_users_created_by_user_id",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_field_hints_users_updated_by_user_id",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "forgot_password_attempts",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    login_name_input = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    email_input = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forgot_password_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_forgot_password_attempts_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lectures",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    title_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    title_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    location_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    location_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    teams_url = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    attendance_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    code_activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    code_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notes_ar = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lectures", x => x.id);
                    table.CheckConstraint("ck_lectures_code_digits", "attendance_code ~ '^[0-9]{4,8}$'");
                    table.CheckConstraint("ck_lectures_mode", "mode IN ('InPerson', 'Online')");
                    table.CheckConstraint("ck_lectures_time_order", "ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_lectures_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lectures_users_created_by_user_id",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lectures_users_updated_by_user_id",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    title_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    body_ar = table.Column<string>(type: "text", nullable: false),
                    body_en = table.Column<string>(type: "text", nullable: true),
                    link_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    related_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    related_entity_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_type", "type IN ('ProposalSubmitted', 'ProposalDecision', 'CourseApproved', 'ParticipantAdded', 'ParticipantRemoved', 'LectureCreated', 'CourseFinished', 'CertificateIssued', 'CertificateRevoked', 'EvaluationReopened', 'ImpactSubmitted', 'ImpactDecision', 'CancellationRequested', 'CancellationDecision', 'CourseCancelled', 'CourseDisabled', 'CourseRestored', 'NeedSubmitted', 'NeedDecision', 'ExtInstructorRequested', 'ExtInstructorReady', 'ProCertSubmitted', 'ProCertApproved', 'ProCertRejected', 'SyncReviewItems')");
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "professional_cert_categories",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professional_cert_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_professional_cert_categories_users_created_by_user_id",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_professional_cert_categories_users_updated_by_user_id",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    remember_me = table.Column<bool>(type: "boolean", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    replaced_by_id = table.Column<long>(type: "bigint", nullable: true),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_agent = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_refresh_tokens_replaced_by_id",
                        column: x => x.replaced_by_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "refresh_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stored_files",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    original_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    stored_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    relative_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "text", nullable: false),
                    uploaded_by = table.Column<long>(type: "bigint", nullable: true),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stored_files", x => x.id);
                    table.CheckConstraint("ck_stored_files_kind", "kind IN ('ProposalCouncilLetter', 'InstructorCv', 'CancellationLetter', 'NeedLetter', 'TrainingMaterial', 'ImpactFile', 'CertificateTemplate', 'CertificatePdf', 'ProfilePhoto', 'ProfessionalCertificate', 'QualityMinutes', 'Recommendation', 'Other')");
                    table.ForeignKey(
                        name: "fk_stored_files_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sync_runs",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    triggered_by = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    stats_json = table.Column<string>(type: "jsonb", nullable: false),
                    error_text = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_runs", x => x.id);
                    table.CheckConstraint("ck_sync_runs_status", "status IN ('Success', 'Partial', 'Failed')");
                    table.ForeignKey(
                        name: "fk_sync_runs_users_triggered_by",
                        column: x => x.triggered_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    setting_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    group_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    default_value = table.Column<string>(type: "text", nullable: false),
                    min_value = table.Column<decimal>(type: "numeric", nullable: true),
                    max_value = table.Column<decimal>(type: "numeric", nullable: true),
                    label_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description_ar = table.Column<string>(type: "text", nullable: true),
                    description_en = table.Column<string>(type: "text", nullable: true),
                    is_secret = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_settings", x => x.setting_key);
                    table.CheckConstraint("ck_system_settings_value_type", "value_type IN ('Int', 'Decimal', 'Bool', 'String', 'Json')");
                    table.ForeignKey(
                        name: "fk_system_settings_users_updated_by",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "training_tracks",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    name_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    description_ar = table.Column<string>(type: "text", nullable: true),
                    description_en = table.Column<string>(type: "text", nullable: true),
                    objectives_ar = table.Column<string>(type: "text", nullable: true),
                    objectives_en = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_tracks", x => x.id);
                    table.ForeignKey(
                        name: "fk_training_tracks_users_created_by_user_id",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_training_tracks_users_updated_by_user_id",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_permission_overrides",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    permission_id = table.Column<long>(type: "bigint", nullable: false),
                    mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_permission_overrides", x => x.id);
                    table.CheckConstraint("ck_user_permission_overrides_mode", "mode IN ('Allow', 'Deny')");
                    table.ForeignKey(
                        name: "fk_user_permission_overrides_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_permission_overrides_users_created_by_user_id",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_permission_overrides_users_updated_by_user_id",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_permission_overrides_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    scope_center_id = table.Column<long>(type: "bigint", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_roles_centers_scope_center_id",
                        column: x => x.scope_center_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_roles_users_created_by_user_id",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_roles_users_updated_by_user_id",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_questions",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    version_id = table.Column<long>(type: "bigint", nullable: false),
                    question_key = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    text_ar = table.Column<string>(type: "text", nullable: false),
                    text_en = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluation_questions_evaluation_form_versions_version_id",
                        column: x => x.version_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "evaluation_form_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_responses",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    participant_id = table.Column<long>(type: "bigint", nullable: false),
                    version_id = table.Column<long>(type: "bigint", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_responses", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluation_responses_course_participants_participant_id",
                        column: x => x.participant_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "course_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evaluation_responses_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evaluation_responses_evaluation_form_versions_version_id",
                        column: x => x.version_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "evaluation_form_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_result_history",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    exam_result_id = table.Column<long>(type: "bigint", nullable: false),
                    old_outcome = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    new_outcome = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    changed_by = table.Column<long>(type: "bigint", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_result_history", x => x.id);
                    table.CheckConstraint("ck_exam_result_history_new_outcome", "new_outcome IN ('Passed', 'Failed')");
                    table.CheckConstraint("ck_exam_result_history_old_outcome", "old_outcome IN ('Passed', 'Failed')");
                    table.ForeignKey(
                        name: "fk_exam_result_history_exam_results_exam_result_id",
                        column: x => x.exam_result_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "exam_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exam_result_history_users_changed_by",
                        column: x => x.changed_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_instructor_profiles",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    organization_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    organization_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cv_file_id = table.Column<long>(type: "bigint", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_from_request_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_instructor_profiles", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_external_instructor_profiles_external_instructor_account_re",
                        column: x => x.created_from_request_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "external_instructor_account_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_instructor_profiles_stored_files_cv_file_id",
                        column: x => x.cv_file_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_instructor_profiles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "impact_submissions",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    participant_id = table.Column<long>(type: "bigint", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    impact_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    file_id = table.Column<long>(type: "bigint", nullable: false),
                    file_sha256 = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by = table.Column<long>(type: "bigint", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    instructor_notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_impact_submissions", x => x.id);
                    table.CheckConstraint("ck_impact_submissions_impact_type", "impact_type IN ('StudyPlan', 'ExamModel', 'ClassActivity')");
                    table.CheckConstraint("ck_impact_submissions_status", "status IN ('Submitted', 'Returned', 'Approved')");
                    table.ForeignKey(
                        name: "fk_impact_submissions_course_participants_participant_id",
                        column: x => x.participant_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "course_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_impact_submissions_stored_files_file_id",
                        column: x => x.file_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_impact_submissions_users_created_by_user_id",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_impact_submissions_users_decided_by",
                        column: x => x.decided_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_impact_submissions_users_updated_by_user_id",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_profiles",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    bio_ar = table.Column<string>(type: "text", nullable: true),
                    bio_en = table.Column<string>(type: "text", nullable: true),
                    photo_file_id = table.Column<long>(type: "bigint", nullable: true),
                    expertise_ar = table.Column<string[]>(type: "text[]", nullable: false),
                    expertise_en = table.Column<string[]>(type: "text[]", nullable: false),
                    contact_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    contact_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    show_in_directory = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_profiles", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_member_profiles_stored_files_photo_file_id",
                        column: x => x.photo_file_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_member_profiles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "professional_certifications",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    issuer_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    issuer_website_url = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    credential_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    verify_url = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    issued_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    file_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by = table.Column<long>(type: "bigint", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_notes = table.Column<string>(type: "text", nullable: true),
                    search_text = table.Column<string>(type: "character varying(900)", maxLength: 900, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professional_certifications", x => x.id);
                    table.CheckConstraint("ck_procert_evidence", "credential_code IS NOT NULL OR verify_url IS NOT NULL");
                    table.CheckConstraint("ck_procert_expiry", "expiry_date IS NULL OR expiry_date >= issued_date");
                    table.CheckConstraint("ck_procert_verify_url", "verify_url IS NULL OR verify_url ~* '^https?://'");
                    table.CheckConstraint("ck_professional_certifications_status", "status IN ('Submitted', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "fk_professional_certifications_professional_cert_categorys_cat",
                        column: x => x.category_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "professional_cert_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_professional_certifications_stored_files_file_id",
                        column: x => x.file_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_professional_certifications_users_created_by_user_id",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_professional_certifications_users_decided_by",
                        column: x => x.decided_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_professional_certifications_users_updated_by_user_id",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_professional_certifications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vault_documents",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    file_id = table.Column<long>(type: "bigint", nullable: false),
                    doc_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    related_course_id = table.Column<long>(type: "bigint", nullable: true),
                    related_center_id = table.Column<long>(type: "bigint", nullable: true),
                    source_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_entity_id = table.Column<long>(type: "bigint", nullable: true),
                    uploaded_by = table.Column<long>(type: "bigint", nullable: true),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vault_documents", x => x.id);
                    table.CheckConstraint("ck_vault_documents_doc_type", "doc_type IN ('ProposalCouncilLetter', 'InstructorCv', 'CancellationLetter', 'NeedLetter', 'TrainingMaterial', 'ImpactFile', 'CertificateTemplate', 'CertificatePdf', 'ProfilePhoto', 'ProfessionalCertificate', 'QualityMinutes', 'Recommendation', 'Other')");
                    table.ForeignKey(
                        name: "fk_vault_documents_centers_related_center_id",
                        column: x => x.related_center_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vault_documents_courses_related_course_id",
                        column: x => x.related_course_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vault_documents_stored_files_file_id",
                        column: x => x.file_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vault_documents_users_created_by_user_id",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vault_documents_users_updated_by_user_id",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vault_documents_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sync_review_items",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    entity_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entity_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    details = table.Column<string>(type: "text", nullable: true),
                    first_seen_run_id = table.Column<long>(type: "bigint", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by = table.Column<long>(type: "bigint", nullable: true),
                    resolution = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_review_items", x => x.id);
                    table.CheckConstraint("ck_sync_review_items_reason", "reason IN ('MissingFromSource', 'UnresolvedReference', 'DuplicateLoginName', 'DuplicateSourceKey', 'SharedEmail', 'StatusChanged')");
                    table.CheckConstraint("ck_sync_review_items_resolution", "resolution IN ('Keep', 'Disable', 'Ignore')");
                    table.ForeignKey(
                        name: "fk_sync_review_items_sync_runs_first_seen_run_id",
                        column: x => x.first_seen_run_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "sync_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sync_review_items_users_resolved_by",
                        column: x => x.resolved_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "training_needs",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    center_id = table.Column<long>(type: "bigint", nullable: false),
                    submitted_by = table.Column<long>(type: "bigint", nullable: false),
                    title_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    description_ar = table.Column<string>(type: "text", nullable: false),
                    description_en = table.Column<string>(type: "text", nullable: true),
                    track_id = table.Column<long>(type: "bigint", nullable: true),
                    expected_participants = table.Column<int>(type: "integer", nullable: true),
                    preferred_timeframe_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requested_instructor_user_id = table.Column<long>(type: "bigint", nullable: true),
                    requested_instructor_note_ar = table.Column<string>(type: "text", nullable: true),
                    letter_file_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_revision = table.Column<int>(type: "integer", nullable: false),
                    last_return_notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<long>(type: "bigint", nullable: true),
                    deactivation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_needs", x => x.id);
                    table.CheckConstraint("ck_training_needs_status", "status IN ('Submitted', 'Returned', 'Approved')");
                    table.ForeignKey(
                        name: "fk_training_needs_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_training_needs_stored_files_letter_file_id",
                        column: x => x.letter_file_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_training_needs_training_tracks_track_id",
                        column: x => x.track_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "training_tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_training_needs_users_created_by_user_id",
                        column: x => x.created_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_training_needs_users_requested_instructor_user_id",
                        column: x => x.requested_instructor_user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_training_needs_users_submitted_by",
                        column: x => x.submitted_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_training_needs_users_updated_by_user_id",
                        column: x => x.updated_by,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_options",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    question_id = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    label_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    label_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    score = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluation_options_evaluation_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "evaluation_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "professional_cert_events",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    cert_id = table.Column<long>(type: "bigint", nullable: false),
                    @event = table.Column<string>(name: "event", type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professional_cert_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_professional_cert_events_professional_certifications_cert_id",
                        column: x => x.cert_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "professional_certifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_professional_cert_events_users_by_user_id",
                        column: x => x.by_user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "training_need_events",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    need_id = table.Column<long>(type: "bigint", nullable: false),
                    @event = table.Column<string>(name: "event", type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_need_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_training_need_events_training_needs_need_id",
                        column: x => x.need_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "training_needs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_training_need_events_users_by_user_id",
                        column: x => x.by_user_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_answers",
                schema: "zulms_rebuild",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    response_id = table.Column<long>(type: "bigint", nullable: false),
                    question_id = table.Column<long>(type: "bigint", nullable: false),
                    option_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_answers", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluation_answers_evaluation_options_option_id",
                        column: x => x.option_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "evaluation_options",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evaluation_answers_evaluation_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "evaluation_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evaluation_answers_evaluation_responses_response_id",
                        column: x => x.response_id,
                        principalSchema: "zulms_rebuild",
                        principalTable: "evaluation_responses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_changes_changed_by",
                schema: "zulms_rebuild",
                table: "attendance_changes",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_changes_lecture_id",
                schema: "zulms_rebuild",
                table: "attendance_changes",
                column: "lecture_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_changes_participant_id",
                schema: "zulms_rebuild",
                table: "attendance_changes",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_code_attempts_lecture_id_user_id",
                schema: "zulms_rebuild",
                table: "attendance_code_attempts",
                columns: new[] { "lecture_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_code_attempts_user_id",
                schema: "zulms_rebuild",
                table: "attendance_code_attempts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_lecture_id_participant_id",
                schema: "zulms_rebuild",
                table: "attendance_records",
                columns: new[] { "lecture_id", "participant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_marked_by",
                schema: "zulms_rebuild",
                table: "attendance_records",
                column: "marked_by");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_participant_id",
                schema: "zulms_rebuild",
                table: "attendance_records",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_action_code",
                schema: "zulms_rebuild",
                table: "audit_logs",
                column: "action_code");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_user_id_occurred_at",
                schema: "zulms_rebuild",
                table: "audit_logs",
                columns: new[] { "actor_user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_category_occurred_at",
                schema: "zulms_rebuild",
                table: "audit_logs",
                columns: new[] { "category", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_course_id_occurred_at",
                schema: "zulms_rebuild",
                table: "audit_logs",
                columns: new[] { "course_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_type_entity_id",
                schema: "zulms_rebuild",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_occurred_at",
                schema: "zulms_rebuild",
                table: "audit_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_blocked_passwords_created_by",
                schema: "zulms_rebuild",
                table: "blocked_passwords",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_blocked_passwords_password_normalized",
                schema: "zulms_rebuild",
                table: "blocked_passwords",
                column: "password_normalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_centers_created_by_user_id",
                schema: "zulms_rebuild",
                table: "centers",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_centers_is_faculty",
                schema: "zulms_rebuild",
                table: "centers",
                column: "is_faculty",
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_centers_side_code_center_code",
                schema: "zulms_rebuild",
                table: "centers",
                columns: new[] { "side_code", "center_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_centers_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "centers",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_certificate_templates_created_by_user_id",
                schema: "zulms_rebuild",
                table: "certificate_templates",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_certificate_templates_file_id",
                schema: "zulms_rebuild",
                table: "certificate_templates",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_certificate_templates_is_current",
                schema: "zulms_rebuild",
                table: "certificate_templates",
                column: "is_current",
                unique: true,
                filter: "is_current");

            migrationBuilder.CreateIndex(
                name: "ix_certificate_templates_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "certificate_templates",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_certificate_templates_version_no",
                schema: "zulms_rebuild",
                table: "certificate_templates",
                column: "version_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certificates_course_id",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_created_by_user_id",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_participant_id",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "participant_id",
                unique: true,
                filter: "status = 'Issued'");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_pdf_file_id",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "pdf_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_reference_no",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "reference_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certificates_template_id",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_user_id_status",
                schema: "zulms_rebuild",
                table: "certificates",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_certificates_verification_token",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "verification_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_cancellation_requests_course_id",
                schema: "zulms_rebuild",
                table: "course_cancellation_requests",
                column: "course_id",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_course_cancellation_requests_created_by_user_id",
                schema: "zulms_rebuild",
                table: "course_cancellation_requests",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_cancellation_requests_decided_by",
                schema: "zulms_rebuild",
                table: "course_cancellation_requests",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_cancellation_requests_letter_file_id",
                schema: "zulms_rebuild",
                table: "course_cancellation_requests",
                column: "letter_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_cancellation_requests_requested_by",
                schema: "zulms_rebuild",
                table: "course_cancellation_requests",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_cancellation_requests_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "course_cancellation_requests",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_exams_course_id",
                schema: "zulms_rebuild",
                table: "course_exams",
                column: "course_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_exams_created_by_user_id",
                schema: "zulms_rebuild",
                table: "course_exams",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_exams_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "course_exams",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_files_course_id",
                schema: "zulms_rebuild",
                table: "course_files",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_files_created_by_user_id",
                schema: "zulms_rebuild",
                table: "course_files",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_files_file_id",
                schema: "zulms_rebuild",
                table: "course_files",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_files_lecture_id",
                schema: "zulms_rebuild",
                table: "course_files",
                column: "lecture_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_files_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "course_files",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_participants_added_by_user_id",
                schema: "zulms_rebuild",
                table: "course_participants",
                column: "added_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_participants_course_id_user_id",
                schema: "zulms_rebuild",
                table: "course_participants",
                columns: new[] { "course_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_participants_created_by_user_id",
                schema: "zulms_rebuild",
                table: "course_participants",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_participants_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "course_participants",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_participants_user_id",
                schema: "zulms_rebuild",
                table: "course_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_proposal_revisions_course_id_revision_no",
                schema: "zulms_rebuild",
                table: "course_proposal_revisions",
                columns: new[] { "course_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_proposal_revisions_decided_by",
                schema: "zulms_rebuild",
                table: "course_proposal_revisions",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_proposal_revisions_submitted_by",
                schema: "zulms_rebuild",
                table: "course_proposal_revisions",
                column: "submitted_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_status_history_changed_by",
                schema: "zulms_rebuild",
                table: "course_status_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_course_status_history_course_id_changed_at",
                schema: "zulms_rebuild",
                table: "course_status_history",
                columns: new[] { "course_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_courses_course_no",
                schema: "zulms_rebuild",
                table: "courses",
                column: "course_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_courses_created_by_user_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_courses_evaluation_form_version_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "evaluation_form_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_instructor_user_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "instructor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_is_active_is_archived",
                schema: "zulms_rebuild",
                table: "courses",
                columns: new[] { "is_active", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "ix_courses_proposed_by_user_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "proposed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_proposing_center_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "proposing_center_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_search_text",
                schema: "zulms_rebuild",
                table: "courses",
                column: "search_text")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_courses_status",
                schema: "zulms_rebuild",
                table: "courses",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_courses_target_center_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "target_center_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_track_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_departments_center_id",
                schema: "zulms_rebuild",
                table: "departments",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "ix_departments_created_by_user_id",
                schema: "zulms_rebuild",
                table: "departments",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_departments_side_code_department_code",
                schema: "zulms_rebuild",
                table: "departments",
                columns: new[] { "side_code", "department_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_departments_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "departments",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_email_outbox_related_user_id",
                schema: "zulms_rebuild",
                table: "email_outbox",
                column: "related_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_outbox_status_next_attempt_at",
                schema: "zulms_rebuild",
                table: "email_outbox",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_employees_center_id",
                schema: "zulms_rebuild",
                table: "employees",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_created_by",
                schema: "zulms_rebuild",
                table: "employees",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_employees_department_id",
                schema: "zulms_rebuild",
                table: "employees",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_search_text",
                schema: "zulms_rebuild",
                table: "employees",
                column: "search_text")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_employees_side_code_employee_code",
                schema: "zulms_rebuild",
                table: "employees",
                columns: new[] { "side_code", "employee_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employees_updated_by",
                schema: "zulms_rebuild",
                table: "employees",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_answers_option_id",
                schema: "zulms_rebuild",
                table: "evaluation_answers",
                column: "option_id");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_answers_question_id",
                schema: "zulms_rebuild",
                table: "evaluation_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_answers_response_id_question_id",
                schema: "zulms_rebuild",
                table: "evaluation_answers",
                columns: new[] { "response_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_form_versions_created_by_user_id",
                schema: "zulms_rebuild",
                table: "evaluation_form_versions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_form_versions_published_by",
                schema: "zulms_rebuild",
                table: "evaluation_form_versions",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_form_versions_status",
                schema: "zulms_rebuild",
                table: "evaluation_form_versions",
                column: "status",
                unique: true,
                filter: "status = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_form_versions_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "evaluation_form_versions",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_form_versions_version_no",
                schema: "zulms_rebuild",
                table: "evaluation_form_versions",
                column: "version_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_options_question_id",
                schema: "zulms_rebuild",
                table: "evaluation_options",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_questions_version_id_question_key",
                schema: "zulms_rebuild",
                table: "evaluation_questions",
                columns: new[] { "version_id", "question_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_responses_course_id_participant_id_version_id",
                schema: "zulms_rebuild",
                table: "evaluation_responses",
                columns: new[] { "course_id", "participant_id", "version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_responses_participant_id",
                schema: "zulms_rebuild",
                table: "evaluation_responses",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_responses_version_id",
                schema: "zulms_rebuild",
                table: "evaluation_responses",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_result_history_changed_by",
                schema: "zulms_rebuild",
                table: "exam_result_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_exam_result_history_exam_result_id",
                schema: "zulms_rebuild",
                table: "exam_result_history",
                column: "exam_result_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_created_by_user_id",
                schema: "zulms_rebuild",
                table: "exam_results",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_deactivated_by",
                schema: "zulms_rebuild",
                table: "exam_results",
                column: "deactivated_by");

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_exam_id_participant_id",
                schema: "zulms_rebuild",
                table: "exam_results",
                columns: new[] { "exam_id", "participant_id" },
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_participant_id",
                schema: "zulms_rebuild",
                table: "exam_results",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_recorded_by",
                schema: "zulms_rebuild",
                table: "exam_results",
                column: "recorded_by");

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "exam_results",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_external_instructor_account_requests_course_id",
                schema: "zulms_rebuild",
                table: "external_instructor_account_requests",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_instructor_account_requests_created_by_user_id",
                schema: "zulms_rebuild",
                table: "external_instructor_account_requests",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_external_instructor_account_requests_created_user_id",
                schema: "zulms_rebuild",
                table: "external_instructor_account_requests",
                column: "created_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_instructor_account_requests_forwarded_by",
                schema: "zulms_rebuild",
                table: "external_instructor_account_requests",
                column: "forwarded_by");

            migrationBuilder.CreateIndex(
                name: "ix_external_instructor_account_requests_handled_by",
                schema: "zulms_rebuild",
                table: "external_instructor_account_requests",
                column: "handled_by");

            migrationBuilder.CreateIndex(
                name: "ix_external_instructor_account_requests_requested_by_user_id",
                schema: "zulms_rebuild",
                table: "external_instructor_account_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_instructor_account_requests_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "external_instructor_account_requests",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_external_instructor_profiles_created_from_request_id",
                schema: "zulms_rebuild",
                table: "external_instructor_profiles",
                column: "created_from_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_instructor_profiles_cv_file_id",
                schema: "zulms_rebuild",
                table: "external_instructor_profiles",
                column: "cv_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_field_hints_created_by_user_id",
                schema: "zulms_rebuild",
                table: "field_hints",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_field_hints_field_key_context_key",
                schema: "zulms_rebuild",
                table: "field_hints",
                columns: new[] { "field_key", "context_key" },
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_field_hints_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "field_hints",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_forgot_password_attempts_ip_requested_at",
                schema: "zulms_rebuild",
                table: "forgot_password_attempts",
                columns: new[] { "ip", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_forgot_password_attempts_login_name_input_requested_at",
                schema: "zulms_rebuild",
                table: "forgot_password_attempts",
                columns: new[] { "login_name_input", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_forgot_password_attempts_user_id",
                schema: "zulms_rebuild",
                table: "forgot_password_attempts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_impact_submissions_created_by_user_id",
                schema: "zulms_rebuild",
                table: "impact_submissions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_impact_submissions_decided_by",
                schema: "zulms_rebuild",
                table: "impact_submissions",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_impact_submissions_file_id",
                schema: "zulms_rebuild",
                table: "impact_submissions",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_impact_submissions_participant_id_sequence_no",
                schema: "zulms_rebuild",
                table: "impact_submissions",
                columns: new[] { "participant_id", "sequence_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_impact_submissions_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "impact_submissions",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_lectures_course_id_starts_at",
                schema: "zulms_rebuild",
                table: "lectures",
                columns: new[] { "course_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lectures_created_by_user_id",
                schema: "zulms_rebuild",
                table: "lectures",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_lectures_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "lectures",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_member_profiles_photo_file_id",
                schema: "zulms_rebuild",
                table: "member_profiles",
                column: "photo_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_read_at_created_at",
                schema: "zulms_rebuild",
                table: "notifications",
                columns: new[] { "user_id", "read_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_permissions_perm_key",
                schema: "zulms_rebuild",
                table: "permissions",
                column: "perm_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_professional_cert_categories_created_by_user_id",
                schema: "zulms_rebuild",
                table: "professional_cert_categories",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_professional_cert_categories_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "professional_cert_categories",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_professional_cert_events_by_user_id",
                schema: "zulms_rebuild",
                table: "professional_cert_events",
                column: "by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_professional_cert_events_cert_id",
                schema: "zulms_rebuild",
                table: "professional_cert_events",
                column: "cert_id");

            migrationBuilder.CreateIndex(
                name: "ix_professional_certifications_category_id",
                schema: "zulms_rebuild",
                table: "professional_certifications",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_professional_certifications_created_by_user_id",
                schema: "zulms_rebuild",
                table: "professional_certifications",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_professional_certifications_decided_by",
                schema: "zulms_rebuild",
                table: "professional_certifications",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_professional_certifications_file_id",
                schema: "zulms_rebuild",
                table: "professional_certifications",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_professional_certifications_search_text",
                schema: "zulms_rebuild",
                table: "professional_certifications",
                column: "search_text")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_professional_certifications_status_submitted_at",
                schema: "zulms_rebuild",
                table: "professional_certifications",
                columns: new[] { "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_professional_certifications_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "professional_certifications",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_professional_certifications_user_id_status",
                schema: "zulms_rebuild",
                table: "professional_certifications",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at",
                schema: "zulms_rebuild",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_replaced_by_id",
                schema: "zulms_rebuild",
                table: "refresh_tokens",
                column: "replaced_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "zulms_rebuild",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                schema: "zulms_rebuild",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_id",
                schema: "zulms_rebuild",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_code",
                schema: "zulms_rebuild",
                table: "roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_sha256",
                schema: "zulms_rebuild",
                table: "stored_files",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_stored_name",
                schema: "zulms_rebuild",
                table: "stored_files",
                column: "stored_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_uploaded_by",
                schema: "zulms_rebuild",
                table: "stored_files",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ix_sync_review_items_entity_type_entity_key_reason",
                schema: "zulms_rebuild",
                table: "sync_review_items",
                columns: new[] { "entity_type", "entity_key", "reason" },
                unique: true,
                filter: "resolved_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sync_review_items_first_seen_run_id",
                schema: "zulms_rebuild",
                table: "sync_review_items",
                column: "first_seen_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_sync_review_items_resolved_by",
                schema: "zulms_rebuild",
                table: "sync_review_items",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "ix_sync_runs_triggered_by",
                schema: "zulms_rebuild",
                table: "sync_runs",
                column: "triggered_by");

            migrationBuilder.CreateIndex(
                name: "ix_system_settings_updated_by",
                schema: "zulms_rebuild",
                table: "system_settings",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_training_need_events_by_user_id",
                schema: "zulms_rebuild",
                table: "training_need_events",
                column: "by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_need_events_need_id",
                schema: "zulms_rebuild",
                table: "training_need_events",
                column: "need_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_needs_center_id",
                schema: "zulms_rebuild",
                table: "training_needs",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_needs_created_by_user_id",
                schema: "zulms_rebuild",
                table: "training_needs",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_training_needs_letter_file_id",
                schema: "zulms_rebuild",
                table: "training_needs",
                column: "letter_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_needs_requested_instructor_user_id",
                schema: "zulms_rebuild",
                table: "training_needs",
                column: "requested_instructor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_needs_submitted_by",
                schema: "zulms_rebuild",
                table: "training_needs",
                column: "submitted_by");

            migrationBuilder.CreateIndex(
                name: "ix_training_needs_track_id",
                schema: "zulms_rebuild",
                table: "training_needs",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_needs_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "training_needs",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_training_tracks_created_by_user_id",
                schema: "zulms_rebuild",
                table: "training_tracks",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_training_tracks_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "training_tracks",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_user_permission_overrides_created_by_user_id",
                schema: "zulms_rebuild",
                table: "user_permission_overrides",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_user_permission_overrides_permission_id",
                schema: "zulms_rebuild",
                table: "user_permission_overrides",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_permission_overrides_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "user_permission_overrides",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_user_permission_overrides_user_id_permission_id",
                schema: "zulms_rebuild",
                table: "user_permission_overrides",
                columns: new[] { "user_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_created_by_user_id",
                schema: "zulms_rebuild",
                table: "user_roles",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                schema: "zulms_rebuild",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_scope_center_id",
                schema: "zulms_rebuild",
                table: "user_roles",
                column: "scope_center_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "user_roles",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_user_id",
                schema: "zulms_rebuild",
                table: "user_roles",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_access_override_by",
                schema: "zulms_rebuild",
                table: "users",
                column: "access_override_by");

            migrationBuilder.CreateIndex(
                name: "ix_users_created_by",
                schema: "zulms_rebuild",
                table: "users",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_users_employee_id",
                schema: "zulms_rebuild",
                table: "users",
                column: "employee_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_login_name",
                schema: "zulms_rebuild",
                table: "users",
                column: "login_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_password_changed_by",
                schema: "zulms_rebuild",
                table: "users",
                column: "password_changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_users_search_text",
                schema: "zulms_rebuild",
                table: "users",
                column: "search_text")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_users_updated_by",
                schema: "zulms_rebuild",
                table: "users",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_vault_documents_created_by_user_id",
                schema: "zulms_rebuild",
                table: "vault_documents",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_vault_documents_doc_type",
                schema: "zulms_rebuild",
                table: "vault_documents",
                column: "doc_type");

            migrationBuilder.CreateIndex(
                name: "ix_vault_documents_file_id",
                schema: "zulms_rebuild",
                table: "vault_documents",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_vault_documents_related_center_id",
                schema: "zulms_rebuild",
                table: "vault_documents",
                column: "related_center_id");

            migrationBuilder.CreateIndex(
                name: "ix_vault_documents_related_course_id",
                schema: "zulms_rebuild",
                table: "vault_documents",
                column: "related_course_id");

            migrationBuilder.CreateIndex(
                name: "ix_vault_documents_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "vault_documents",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_vault_documents_uploaded_by",
                schema: "zulms_rebuild",
                table: "vault_documents",
                column: "uploaded_by");

            migrationBuilder.AddForeignKey(
                name: "fk_attendance_changes_course_participants_participant_id",
                schema: "zulms_rebuild",
                table: "attendance_changes",
                column: "participant_id",
                principalSchema: "zulms_rebuild",
                principalTable: "course_participants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_attendance_changes_lectures_lecture_id",
                schema: "zulms_rebuild",
                table: "attendance_changes",
                column: "lecture_id",
                principalSchema: "zulms_rebuild",
                principalTable: "lectures",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_attendance_changes_users_changed_by",
                schema: "zulms_rebuild",
                table: "attendance_changes",
                column: "changed_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_attendance_code_attempts_lectures_lecture_id",
                schema: "zulms_rebuild",
                table: "attendance_code_attempts",
                column: "lecture_id",
                principalSchema: "zulms_rebuild",
                principalTable: "lectures",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_attendance_code_attempts_users_user_id",
                schema: "zulms_rebuild",
                table: "attendance_code_attempts",
                column: "user_id",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_attendance_records_course_participants_participant_id",
                schema: "zulms_rebuild",
                table: "attendance_records",
                column: "participant_id",
                principalSchema: "zulms_rebuild",
                principalTable: "course_participants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_attendance_records_lectures_lecture_id",
                schema: "zulms_rebuild",
                table: "attendance_records",
                column: "lecture_id",
                principalSchema: "zulms_rebuild",
                principalTable: "lectures",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_attendance_records_users_marked_by",
                schema: "zulms_rebuild",
                table: "attendance_records",
                column: "marked_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_logs_courses_course_id",
                schema: "zulms_rebuild",
                table: "audit_logs",
                column: "course_id",
                principalSchema: "zulms_rebuild",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_logs_users_actor_user_id",
                schema: "zulms_rebuild",
                table: "audit_logs",
                column: "actor_user_id",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_blocked_passwords_users_created_by",
                schema: "zulms_rebuild",
                table: "blocked_passwords",
                column: "created_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_centers_users_created_by_user_id",
                schema: "zulms_rebuild",
                table: "centers",
                column: "created_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_centers_users_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "centers",
                column: "updated_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_certificate_templates_stored_files_file_id",
                schema: "zulms_rebuild",
                table: "certificate_templates",
                column: "file_id",
                principalSchema: "zulms_rebuild",
                principalTable: "stored_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_certificate_templates_users_created_by_user_id",
                schema: "zulms_rebuild",
                table: "certificate_templates",
                column: "created_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_certificate_templates_users_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "certificate_templates",
                column: "updated_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_certificates_course_participants_participant_id",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "participant_id",
                principalSchema: "zulms_rebuild",
                principalTable: "course_participants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_certificates_courses_course_id",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "course_id",
                principalSchema: "zulms_rebuild",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_certificates_stored_files_pdf_file_id",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "pdf_file_id",
                principalSchema: "zulms_rebuild",
                principalTable: "stored_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_certificates_users_created_by_user_id",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "created_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_certificates_users_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "updated_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_certificates_users_user_id",
                schema: "zulms_rebuild",
                table: "certificates",
                column: "user_id",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_cancellation_requests_courses_course_id",
                schema: "zulms_rebuild",
                table: "course_cancellation_requests",
                column: "course_id",
                principalSchema: "zulms_rebuild",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_cancellation_requests_stored_files_letter_file_id",
                schema: "zulms_rebuild",
                table: "course_cancellation_requests",
                column: "letter_file_id",
                principalSchema: "zulms_rebuild",
                principalTable: "stored_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_cancellation_requests_users_created_by_user_id",
                schema: "zulms_rebuild",
                table: "course_cancellation_requests",
                column: "created_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_cancellation_requests_users_decided_by",
                schema: "zulms_rebuild",
                table: "course_cancellation_requests",
                column: "decided_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_cancellation_requests_users_requested_by",
                schema: "zulms_rebuild",
                table: "course_cancellation_requests",
                column: "requested_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_cancellation_requests_users_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "course_cancellation_requests",
                column: "updated_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_exams_courses_course_id",
                schema: "zulms_rebuild",
                table: "course_exams",
                column: "course_id",
                principalSchema: "zulms_rebuild",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_exams_users_created_by_user_id",
                schema: "zulms_rebuild",
                table: "course_exams",
                column: "created_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_exams_users_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "course_exams",
                column: "updated_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_files_courses_course_id",
                schema: "zulms_rebuild",
                table: "course_files",
                column: "course_id",
                principalSchema: "zulms_rebuild",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_files_lectures_lecture_id",
                schema: "zulms_rebuild",
                table: "course_files",
                column: "lecture_id",
                principalSchema: "zulms_rebuild",
                principalTable: "lectures",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_files_stored_files_file_id",
                schema: "zulms_rebuild",
                table: "course_files",
                column: "file_id",
                principalSchema: "zulms_rebuild",
                principalTable: "stored_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_files_users_created_by_user_id",
                schema: "zulms_rebuild",
                table: "course_files",
                column: "created_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_files_users_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "course_files",
                column: "updated_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_participants_courses_course_id",
                schema: "zulms_rebuild",
                table: "course_participants",
                column: "course_id",
                principalSchema: "zulms_rebuild",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_participants_users_added_by_user_id",
                schema: "zulms_rebuild",
                table: "course_participants",
                column: "added_by_user_id",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_participants_users_created_by_user_id",
                schema: "zulms_rebuild",
                table: "course_participants",
                column: "created_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_participants_users_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "course_participants",
                column: "updated_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_participants_users_user_id",
                schema: "zulms_rebuild",
                table: "course_participants",
                column: "user_id",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_proposal_revisions_courses_course_id",
                schema: "zulms_rebuild",
                table: "course_proposal_revisions",
                column: "course_id",
                principalSchema: "zulms_rebuild",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_proposal_revisions_users_decided_by",
                schema: "zulms_rebuild",
                table: "course_proposal_revisions",
                column: "decided_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_proposal_revisions_users_submitted_by",
                schema: "zulms_rebuild",
                table: "course_proposal_revisions",
                column: "submitted_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_status_history_courses_course_id",
                schema: "zulms_rebuild",
                table: "course_status_history",
                column: "course_id",
                principalSchema: "zulms_rebuild",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_course_status_history_users_changed_by",
                schema: "zulms_rebuild",
                table: "course_status_history",
                column: "changed_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_courses_evaluation_form_versions_evaluation_form_version_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "evaluation_form_version_id",
                principalSchema: "zulms_rebuild",
                principalTable: "evaluation_form_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_courses_training_tracks_track_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "track_id",
                principalSchema: "zulms_rebuild",
                principalTable: "training_tracks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_courses_users_created_by_user_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "created_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_courses_users_instructor_user_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "instructor_user_id",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_courses_users_proposed_by_user_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "proposed_by_user_id",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_courses_users_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "courses",
                column: "updated_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_departments_users_created_by_user_id",
                schema: "zulms_rebuild",
                table: "departments",
                column: "created_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_departments_users_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "departments",
                column: "updated_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_email_outbox_users_related_user_id",
                schema: "zulms_rebuild",
                table: "email_outbox",
                column: "related_user_id",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_employees_users_created_by",
                schema: "zulms_rebuild",
                table: "employees",
                column: "created_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_employees_users_updated_by",
                schema: "zulms_rebuild",
                table: "employees",
                column: "updated_by",
                principalSchema: "zulms_rebuild",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_centers_users_created_by_user_id",
                schema: "zulms_rebuild",
                table: "centers");

            migrationBuilder.DropForeignKey(
                name: "fk_centers_users_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "centers");

            migrationBuilder.DropForeignKey(
                name: "fk_departments_users_created_by_user_id",
                schema: "zulms_rebuild",
                table: "departments");

            migrationBuilder.DropForeignKey(
                name: "fk_departments_users_updated_by_user_id",
                schema: "zulms_rebuild",
                table: "departments");

            migrationBuilder.DropForeignKey(
                name: "fk_employees_users_created_by",
                schema: "zulms_rebuild",
                table: "employees");

            migrationBuilder.DropForeignKey(
                name: "fk_employees_users_updated_by",
                schema: "zulms_rebuild",
                table: "employees");

            migrationBuilder.DropTable(
                name: "attendance_changes",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "attendance_code_attempts",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "attendance_records",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "blocked_passwords",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "certificates",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "course_cancellation_requests",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "course_files",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "course_proposal_revisions",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "course_status_history",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "email_outbox",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "evaluation_answers",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "exam_result_history",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "external_instructor_profiles",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "field_hints",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "forgot_password_attempts",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "impact_submissions",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "member_profiles",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "number_sequences",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "password_policy",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "professional_cert_events",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "sync_review_items",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "system_settings",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "training_need_events",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "user_permission_overrides",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "vault_documents",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "certificate_templates",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "lectures",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "evaluation_options",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "evaluation_responses",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "exam_results",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "external_instructor_account_requests",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "professional_certifications",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "sync_runs",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "training_needs",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "evaluation_questions",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "course_exams",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "course_participants",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "professional_cert_categories",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "stored_files",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "courses",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "evaluation_form_versions",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "training_tracks",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "users",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "employees",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "departments",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "centers",
                schema: "zulms_rebuild");

            migrationBuilder.DropTable(
                name: "university_sides",
                schema: "zulms_rebuild");
        }
    }
}

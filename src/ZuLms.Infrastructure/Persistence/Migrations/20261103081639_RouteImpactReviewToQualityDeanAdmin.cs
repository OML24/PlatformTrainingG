using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261103081639_RouteImpactReviewToQualityDeanAdmin")]
public sealed class RouteImpactReviewToQualityDeanAdmin : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE zulms_rebuild.permissions
            SET scope = 'Global',
                name_ar = CASE WHEN perm_key = 'page.instructor.impact'
                    THEN 'صفحة — مراجعة قياس الأثر' ELSE name_ar END,
                name_en = CASE WHEN perm_key = 'page.instructor.impact'
                    THEN 'page / impact / review' ELSE name_en END
            WHERE perm_key IN ('page.instructor.impact', 'impact.review');

            DELETE FROM zulms_rebuild.role_permissions rp
            USING zulms_rebuild.roles r, zulms_rebuild.permissions p
            WHERE rp.role_id = r.id AND rp.permission_id = p.id
              AND r.code IN ('Instructor', 'ExternalInstructor')
              AND p.perm_key IN ('page.instructor.impact', 'impact.review');

            INSERT INTO zulms_rebuild.role_permissions (role_id, permission_id)
            SELECT r.id, p.id
            FROM zulms_rebuild.roles r
            CROSS JOIN zulms_rebuild.permissions p
            WHERE r.code = 'Dean'
              AND p.perm_key IN ('page.instructor.impact', 'impact.review')
            ON CONFLICT (role_id, permission_id) DO NOTHING;
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM zulms_rebuild.role_permissions rp
            USING zulms_rebuild.roles r, zulms_rebuild.permissions p
            WHERE rp.role_id = r.id AND rp.permission_id = p.id
              AND r.code = 'Dean'
              AND p.perm_key IN ('page.instructor.impact', 'impact.review');

            INSERT INTO zulms_rebuild.role_permissions (role_id, permission_id)
            SELECT r.id, p.id
            FROM zulms_rebuild.roles r
            CROSS JOIN zulms_rebuild.permissions p
            WHERE r.code IN ('Instructor', 'ExternalInstructor')
              AND p.perm_key IN ('page.instructor.impact', 'impact.review')
            ON CONFLICT (role_id, permission_id) DO NOTHING;

            UPDATE zulms_rebuild.permissions
            SET scope = 'InstructedCourse',
                name_ar = CASE WHEN perm_key = 'page.instructor.impact'
                    THEN 'صفحة — المحاضر — الأثر التدريبي' ELSE name_ar END,
                name_en = CASE WHEN perm_key = 'page.instructor.impact'
                    THEN 'page / instructor / impact' ELSE name_en END
            WHERE perm_key IN ('page.instructor.impact', 'impact.review');
            """);
    }
}

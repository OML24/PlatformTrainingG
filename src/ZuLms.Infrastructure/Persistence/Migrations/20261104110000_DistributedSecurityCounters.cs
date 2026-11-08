using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261104110000_DistributedSecurityCounters")]
public sealed class DistributedSecurityCounters : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE zulms_rebuild.rate_windows (
                bucket varchar(128) PRIMARY KEY,
                window_start timestamptz NOT NULL,
                request_count integer NOT NULL
            );
            CREATE TABLE zulms_rebuild.credential_reauth_grants (
                user_id bigint NOT NULL,
                session_id varchar(128) NOT NULL,
                expires_at timestamptz NOT NULL,
                PRIMARY KEY (user_id, session_id)
            );
            CREATE INDEX ix_credential_reauth_expires
                ON zulms_rebuild.credential_reauth_grants (expires_at);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE zulms_rebuild.credential_reauth_grants;
            DROP TABLE zulms_rebuild.rate_windows;
            """);
    }
}

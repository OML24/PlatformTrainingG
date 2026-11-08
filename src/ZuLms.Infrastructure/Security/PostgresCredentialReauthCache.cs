using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Infrastructure.Security;

public sealed class PostgresCredentialReauthCache(AppDbContext db) : ICredentialReauthCache
{
    public async Task GrantAsync(long userId, string sessionId, DateTimeOffset expiresAt,
        CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO zulms_rebuild.credential_reauth_grants (user_id, session_id, expires_at)
            VALUES ({userId}, {sessionId}, {expiresAt})
            ON CONFLICT (user_id, session_id) DO UPDATE SET expires_at = EXCLUDED.expires_at;
            """, ct);
    }

    public Task<bool> IsGrantedAsync(long userId, string sessionId, CancellationToken ct) =>
        db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM zulms_rebuild.credential_reauth_grants
                WHERE user_id = {userId} AND session_id = {sessionId}
                  AND expires_at > now()) AS "Value"
            """).SingleAsync(ct);

    public async Task InvalidateAsync(long userId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM zulms_rebuild.credential_reauth_grants WHERE user_id = {userId}
            """, ct);
    }
}

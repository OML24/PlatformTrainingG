using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Web.Security;

public sealed class LoginRateGate(IClock clock, AppDbContext db)
{
    public async Task<bool> AllowAsync(string ip, int limit, string endpoint,
        CancellationToken ct)
    {
        string bucket = $"{endpoint}:{ip}";
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO zulms_rebuild.rate_windows (bucket, window_start, request_count)
                VALUES (@bucket, @now, 1)
                ON CONFLICT (bucket) DO UPDATE SET
                    window_start = CASE WHEN rate_windows.window_start <= @now - interval '1 minute'
                        THEN @now ELSE rate_windows.window_start END,
                    request_count = CASE WHEN rate_windows.window_start <= @now - interval '1 minute'
                        THEN 1 ELSE rate_windows.request_count + 1 END
                RETURNING request_count;
                """;
            var key = command.CreateParameter(); key.ParameterName = "bucket"; key.Value = bucket;
            var now = command.CreateParameter(); now.ParameterName = "now"; now.Value = clock.UtcNow;
            command.Parameters.Add(key); command.Parameters.Add(now);
            int count = (int)(await command.ExecuteScalarAsync(ct))!;
            return count <= limit;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}

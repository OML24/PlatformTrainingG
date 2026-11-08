using Npgsql;

namespace ZuLms.Infrastructure.University;

public sealed class UniversitySyncLock
{
    private const long LockKey = 0x5A55_5359_4E43L;

    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken ct)
    {
        string connection = Environment.GetEnvironmentVariable("ZU_DB_CONNECTION")
            ?? throw new InvalidOperationException("ZU_DB_CONNECTION is required.");
        var db = new NpgsqlConnection(connection);
        await db.OpenAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", db);
            command.Parameters.AddWithValue("key", LockKey);
            bool acquired = (bool)(await command.ExecuteScalarAsync(ct) ?? false);
            if (acquired) return new Lease(db);
            await db.DisposeAsync();
            return null;
        }
        catch
        {
            await db.DisposeAsync();
            throw;
        }
    }

    private sealed class Lease(NpgsqlConnection db) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", db);
                command.Parameters.AddWithValue("key", LockKey);
                await command.ExecuteScalarAsync();
            }
            finally { await db.DisposeAsync(); }
        }
    }
}

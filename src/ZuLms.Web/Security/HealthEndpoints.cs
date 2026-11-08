using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Infrastructure.Persistence;

namespace ZuLms.Web.Security;

/// <summary>Liveness/readiness endpoint: verifies database connectivity and storage access.</summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", async Task<IResult> (AppDbContext db,
            IFileStorage storage) =>
        {
            try
            {
                await db.Database.OpenConnectionAsync();
                await db.Database.CloseConnectionAsync();
            }
            catch
            {
                return Results.Json(new { status = "unhealthy", database = "down" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            string? probe = null;
            try
            {
                probe = await storage.WriteAsync(".txt", "ok"u8.ToArray(),
                    DateTimeOffset.UtcNow, CancellationToken.None);
                await using (var stream = await storage.OpenReadAsync(probe, CancellationToken.None))
                {
                    if (stream.ReadByte() != (byte)'o') throw new IOException("Storage probe mismatch.");
                }
                await storage.DeleteUncommittedAsync(probe);
            }
            catch
            {
                if (probe is not null)
                {
                    try { await storage.DeleteUncommittedAsync(probe); }
                    catch (IOException) { /* preserve the unhealthy response */ }
                }
                return Results.Json(new { status = "unhealthy", storage = "down" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            return Results.Ok(new { status = "healthy" });
        });
    }
}

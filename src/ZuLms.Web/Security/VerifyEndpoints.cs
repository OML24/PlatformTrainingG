using Microsoft.AspNetCore.Http.HttpResults;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Features.Certificates;

namespace ZuLms.Web.Security;

/// <summary>Public, rate-limited certificate verification endpoint (no authentication).</summary>
public static class VerifyEndpoints
{
    public static void MapVerifyEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/public/verify/{reference}", async Task<IResult> (
            string reference, ICertificateService certificates,
            ISettingsService settings, LoginRateGate rate, HttpContext http) =>
        {
            if (!await rate.AllowAsync(http.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                await settings.GetAsync<int>("cert.verify_rate_per_minute",
                    http.RequestAborted), "verify", http.RequestAborted))
                return Results.Json(new { code = "RATE_LIMITED" },
                    statusCode: StatusCodes.Status429TooManyRequests);
            var result = await certificates.VerifyAsync(reference, http.RequestAborted);
            // A null certificate intentionally answers with no details at all.
            return Results.Ok(result.Value);
        });
    }
}

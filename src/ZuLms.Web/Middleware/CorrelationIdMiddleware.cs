using System.Globalization;

namespace ZuLms.Web.Middleware;

/// <summary>
/// Adds a correlation id to every request/response and to the trace identifier used in logs.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        string id = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
            id = Guid.NewGuid().ToString("N");
        context.TraceIdentifier = id;
        context.Response.Headers[HeaderName] = id;
        await next(context);
    }
}

/// <summary>
/// Catches unhandled exceptions. API paths receive the JSON error envelope, pages receive a safe error page.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client closed the connection; nothing to write.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception while processing {Method} {Path} (correlation {CorrelationId})",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                await context.Response.WriteAsJsonAsync(new
                {
                    ok = false,
                    code = "INTERNAL_ERROR",
                    message = ApiMessages.Error("INTERNAL_ERROR", CultureInfo.CurrentUICulture)
                });
                return;
            }
            string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            string dir = lang == "ar" ? "rtl" : "ltr";
            string title = lang == "ar" ? "حدث خطأ غير متوقع" : "An unexpected error occurred";
            string body = lang == "ar"
                ? "تعذّر إكمال الطلب. جرّب مرة أخرى، وإن تكرّر الخطأ فأبلغ مسؤول النظام."
                : "The request could not be completed. Try again, and contact the administrator if it persists.";
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync($"""
                <!doctype html><html lang="{lang}" dir="{dir}"><head><meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>{title} — ZU-LMS</title></head>
                <body style="font-family:system-ui,sans-serif;padding:3rem;text-align:center">
                <h1 style="font-size:1.4rem">{title}</h1><p>{body}</p>
                <p style="color:#666;font-size:.8rem">#{context.TraceIdentifier}</p>
                <p><a href="/">ZU-LMS</a></p></body></html>
                """);
        }
    }
}

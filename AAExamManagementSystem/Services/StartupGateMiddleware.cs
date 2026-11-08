using System.Net;

namespace AAExamManagementSystem.Services;

/// <summary>
/// While the database is still being prepared, answers every request except /healthz with a short "starting up"
/// page (503 and Retry-After) instead of letting it fail against tables that do not exist yet.
/// </summary>
public class StartupGateMiddleware
{
    private const string StartingPage =
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
        "<meta http-equiv=\"refresh\" content=\"5\"><title>Starting up</title>" +
        "<style>body{font-family:system-ui,sans-serif;display:flex;min-height:100vh;align-items:center;" +
        "justify-content:center;margin:0;background:#f5f6fa;color:#333;text-align:center}main{padding:24px}</style>" +
        "</head><body><main><h1>Ad Astra Exams is starting up</h1>" +
        "<p>The system is getting ready. This page refreshes on its own.</p></main></body></html>";

    private const string FailedPage =
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Unavailable</title>" +
        "<style>body{font-family:system-ui,sans-serif;display:flex;min-height:100vh;align-items:center;" +
        "justify-content:center;margin:0;background:#f5f6fa;color:#333;text-align:center}main{padding:24px}</style>" +
        "</head><body><main><h1>The system is unavailable</h1>" +
        "<p>It could not reach its database. Please try again in a few minutes.</p></main></body></html>";

    private readonly RequestDelegate _next;
    private readonly DatabaseInitializationState _state;

    public StartupGateMiddleware(RequestDelegate next, DatabaseInitializationState state)
    {
        _next = next;
        _state = state;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_state.IsReady || context.Request.Path.StartsWithSegments("/healthz"))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
        context.Response.Headers.RetryAfter = "5";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(_state.HasFailed ? FailedPage : StartingPage);
    }
}

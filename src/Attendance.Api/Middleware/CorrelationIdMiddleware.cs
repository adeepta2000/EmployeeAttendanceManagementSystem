using Serilog.Context;

namespace Attendance.Api.Middleware;

/// <summary>
/// Gives every request a correlation ID: the caller's <c>X-Correlation-ID</c> header when it is
/// well-formed (so the Next.js BFF can pass its own through), otherwise a new one. The ID is pushed
/// onto every log event of the request, set as <see cref="HttpContext.TraceIdentifier"/> and echoed
/// in the response header.
/// </summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string LogProperty = "CorrelationId";

    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsWellFormed(supplied) ? supplied : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;

        // Set when the response starts rather than now: the exception handler clears headers before
        // writing an error response, and the ID matters most on those.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(LogProperty, correlationId))
        {
            await next(context);
        }
    }

    // A restrictive alphabet keeps a hostile header from forging log lines or bloating every entry.
    private static bool IsWellFormed(string value) =>
        value.Length is > 0 and <= MaxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

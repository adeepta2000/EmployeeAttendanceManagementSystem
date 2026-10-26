using System.Globalization;
using System.Threading.RateLimiting;
using Attendance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Attendance.Api.Setup;

internal static class RateLimitingSetup
{
    /// <summary>Policy name for <c>[EnableRateLimiting]</c> on the sign-in endpoint.</summary>
    public const string LoginPolicy = "login";

    /// <summary>Policy name for <c>[EnableRateLimiting]</c> on the forgot- and reset-password endpoints.</summary>
    public const string PasswordResetPolicy = "password-reset";

    /// <summary>Sign-in attempts allowed per client IP per minute (spec §2.6).</summary>
    public const int LoginPermitsPerMinute = 5;

    /// <summary>
    /// Reset requests and reset attempts allowed per client IP per minute. Enough for someone who mistypes
    /// their address; too few to mail-bomb an inbox or to sweep addresses.
    /// </summary>
    public const int PasswordResetPermitsPerMinute = 5;

    private static readonly Dictionary<string, string> RejectionMessages = new()
    {
        [LoginPolicy] = "Too many sign-in attempts. Wait a minute and try again.",
        [PasswordResetPolicy] = "Too many password-reset requests. Wait a minute and try again.",
    };

    /// <summary>
    /// Throttles the anonymous auth endpoints per client IP with the built-in rate limiter. This slows
    /// guessing from one address; the per-account lockout covers guessing spread over many addresses.
    /// </summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // A sliding window (six 10-second segments) never admits more than the limit in any minute,
            // unlike a fixed window, which allows twice the limit across a window boundary.
            options.AddPolicy(LoginPolicy, context => PerClientIp(context, LoginPermitsPerMinute));
            options.AddPolicy(PasswordResetPolicy, context => PerClientIp(context, PasswordResetPermitsPerMinute));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = policy is not null && RejectionMessages.TryGetValue(policy, out var message)
                            ? message
                            : "Too many requests. Wait a minute and try again.",
                    },
                });
            };
        });

    // Each policy gets its own partitions, so sign-in attempts and reset requests don't share a budget.
    private static RateLimitPartition<string> PerClientIp(HttpContext context, int permitsPerMinute) =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.GetClientIp(),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
            });
}

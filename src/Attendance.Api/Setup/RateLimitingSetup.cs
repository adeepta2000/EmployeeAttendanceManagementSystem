using System.Globalization;
using System.Threading.RateLimiting;
using Attendance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Setup;

internal static class RateLimitingSetup
{
    /// <summary>Policy name for <c>[EnableRateLimiting]</c> on the sign-in endpoint.</summary>
    public const string LoginPolicy = "login";

    /// <summary>Sign-in attempts allowed per client IP per minute (spec §2.6).</summary>
    public const int LoginPermitsPerMinute = 5;

    /// <summary>
    /// Throttles sign-in per client IP with the built-in rate limiter. This slows guessing from one
    /// address; the per-account lockout covers guessing spread over many addresses.
    /// </summary>
    public static IServiceCollection AddLoginRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // A sliding window (six 10-second segments) never admits more than 5 in any minute,
            // unlike a fixed window, which allows a burst of 10 across a window boundary.
            options.AddPolicy(LoginPolicy, context => RateLimitPartition.GetSlidingWindowLimiter(
                context.GetClientIp(),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = LoginPermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "Too many sign-in attempts. Wait a minute and try again.",
                    },
                });
            };
        });
}

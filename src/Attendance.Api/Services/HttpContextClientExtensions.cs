using Attendance.Domain.Identity;

namespace Attendance.Api.Services;

/// <summary>Request metadata recorded on refresh tokens and used as the rate-limit partition.</summary>
internal static class HttpContextClientExtensions
{
    /// <summary>
    /// The client's IP address. Behind a proxy it is only correct when the proxy is trusted by the
    /// forwarded-headers middleware (by default, loopback only).
    /// </summary>
    public static string GetClientIp(this HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return Truncate(address.ToString(), RefreshToken.IpAddressMaxLength);
    }

    public static string? GetUserAgent(this HttpContext context)
    {
        var userAgent = context.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(userAgent) ? null : Truncate(userAgent, RefreshToken.UserAgentMaxLength);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

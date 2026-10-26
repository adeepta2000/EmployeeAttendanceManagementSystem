using System.ComponentModel.DataAnnotations;

namespace Attendance.Application.Identity;

/// <summary>Token lifetimes and lockout policy (spec §2.5, §2.6). Bound from the <c>Auth</c> section.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How long each refresh token lives. Every rotation issues a fresh one, so an active session slides.</summary>
    [Range(typeof(TimeSpan), "01:00:00", "90.00:00:00")]
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);

    [Range(1, 100)]
    public int MaxFailedLoginAttempts { get; set; } = 5;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How long an emailed password-reset link works (screen 3). It also works only once.</summary>
    [Range(typeof(TimeSpan), "00:05:00", "1.00:00:00")]
    public TimeSpan PasswordResetLinkLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The web client's public address, e.g. <c>https://attendance.nordwin.example</c>. Links in emails
    /// point at it. Required: there is no safe default outside Development.
    /// </summary>
    [Required]
    [Url]
    public string ClientBaseUrl { get; set; } = string.Empty;
}

/// <summary>Access-token signing and validation settings. Bound from the <c>Jwt</c> section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 key, at least 32 characters. It is a secret: set it with user-secrets or the
    /// <c>Jwt__SigningKey</c> environment variable, never in a committed appsettings file.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;
}

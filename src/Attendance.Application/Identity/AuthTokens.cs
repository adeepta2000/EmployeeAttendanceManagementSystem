namespace Attendance.Application.Identity;

/// <summary>
/// The token pair returned by sign-in and refresh. The API returns it in the response body; the
/// Next.js Route Handlers move it into httpOnly cookies (spec §2.5).
/// </summary>
/// <param name="MustChangePassword">True when the user must change their password before doing anything else (screen 4).</param>
public sealed record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresUtc,
    bool MustChangePassword)
{
    // Keeps the tokens out of logs and exception messages.
    public override string ToString() => nameof(AuthTokens);
}

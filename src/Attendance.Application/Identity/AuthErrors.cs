using Attendance.Shared.Results;

namespace Attendance.Application.Identity;

public static class AuthErrors
{
    /// <summary>
    /// Every failed sign-in gets this one error, whether the email is unknown, the password is wrong or
    /// the account is locked or inactive, so a caller can't use it to find out which accounts exist.
    /// </summary>
    public static readonly Error InvalidCredentials = new(
        "auth.invalid_credentials", "Invalid credentials.", ErrorType.Unauthorized);

    /// <summary>The refresh token is unknown, expired or revoked. The client must sign in again.</summary>
    public static readonly Error InvalidRefreshToken = new(
        "auth.invalid_refresh_token", "The session has expired or was revoked. Sign in again.", ErrorType.Unauthorized);
}

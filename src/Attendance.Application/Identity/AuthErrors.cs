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

    /// <summary>
    /// Changing the password needs the current one. A validation error, not 401: the caller is signed in,
    /// and a 401 would tell the client the session is over.
    /// </summary>
    public static readonly Error InvalidCurrentPassword = new(
        "auth.invalid_current_password",
        "The current password is wrong. Check it and try again.",
        ErrorType.Validation);

    /// <summary>Too many wrong passwords: the account is locked, for changing the password as for signing in.</summary>
    public static Error LockedOut(TimeSpan lockoutDuration) => new(
        "auth.locked_out",
        $"Too many wrong passwords. Wait {Math.Ceiling(lockoutDuration.TotalMinutes):0} minutes, then try again.",
        ErrorType.Validation);

    /// <summary>
    /// The reset link is unknown, used, replaced by a newer one or expired, or its account is inactive.
    /// One error for all, so the response reveals nothing about which.
    /// </summary>
    public static readonly Error InvalidResetToken = new(
        "auth.invalid_reset_token",
        "This reset link is invalid, has already been used or has expired. Request a new one.",
        ErrorType.Validation);

    /// <summary>The account must change its password before it may do anything else (screen 4).</summary>
    public static readonly Error PasswordChangeRequired = new(
        "auth.password_change_required",
        "Change your password before doing anything else.",
        ErrorType.Forbidden);
}

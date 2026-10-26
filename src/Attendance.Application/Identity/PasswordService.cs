using Attendance.Application.Identity.Abstractions;
using Attendance.Domain.Identity;
using FluentValidation;

namespace Attendance.Application.Identity;

/// <summary>Sets a user's password, whether they changed it themselves or used a reset link.</summary>
internal sealed class PasswordService(
    IPasswordHasher passwordHasher,
    IRefreshTokenRepository refreshTokens,
    IPasswordResetTokenRepository resetTokens)
{
    /// <summary>
    /// Stores the new password and ends everything that would still let someone in on the old one: every
    /// session of the account (all refresh-token families) and every outstanding reset link. Clears the
    /// must-change flag and any lockout. Nothing is saved: the caller commits it with its own changes.
    /// </summary>
    /// <returns>How many sessions were ended.</returns>
    public async Task<int> ReplaceAsync(User user, string newPassword, DateTime nowUtc, CancellationToken cancellationToken)
    {
        user.SetPassword(passwordHasher.Hash(newPassword));
        user.ClearLockout();

        var sessions = await refreshTokens.GetUnrevokedForUserAsync(user.Id, cancellationToken);
        foreach (var token in sessions)
        {
            token.Revoke(nowUtc);
        }

        foreach (var link in await resetTokens.GetOutstandingForUserAsync(user.Id, cancellationToken))
        {
            link.Revoke(nowUtc);
        }

        return sessions.Select(t => t.FamilyId).Distinct().Count();
    }
}

internal static class PasswordValidationRules
{
    /// <summary>The password policy for a new password: its length, and nothing else (see <see cref="AuthInputLimits"/>).</summary>
    public static IRuleBuilderOptions<T, string> MustBeNewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Length(AuthInputLimits.NewPasswordMinLength, AuthInputLimits.PasswordMaxLength);
}

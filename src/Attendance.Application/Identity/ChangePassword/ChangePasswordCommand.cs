using Attendance.Application.Common.Messaging;
using Attendance.Domain.Identity;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Identity.ChangePassword;

/// <summary>
/// Changes the signed-in user's password (screen 4, forced on first sign-in when the account must change
/// it). Every other session ends; the caller gets a fresh one.
/// </summary>
/// <param name="ClientIp">The caller's IP address, recorded on the new refresh token.</param>
/// <param name="UserAgent">The caller's user agent, recorded on the new refresh token.</param>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword, string ClientIp, string? UserAgent)
    : ICommand<Result<AuthTokens>>
{
    // Keeps both passwords out of logs and exception messages.
    public override string ToString() => nameof(ChangePasswordCommand);
}

internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty().MaximumLength(AuthInputLimits.PasswordMaxLength);
        RuleFor(c => c.NewPassword).MustBeNewPassword();
        RuleFor(c => c.NewPassword)
            .NotEqual(c => c.CurrentPassword, StringComparer.Ordinal)
            .WithMessage("Choose a new password that is different from the current one.");
        RuleFor(c => c.ClientIp).NotEmpty().MaximumLength(RefreshToken.IpAddressMaxLength);
        RuleFor(c => c.UserAgent).MaximumLength(RefreshToken.UserAgentMaxLength);
    }
}

using Attendance.Application.Common.Messaging;
using Attendance.Domain.Identity;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Identity.ResetPassword;

/// <summary>
/// Sets a new password with the one-time token of an emailed reset link (screen 3). It signs the account
/// out everywhere and lifts any lockout; the user then signs in with the new password.
/// </summary>
public sealed record ResetPasswordCommand(string Token, string NewPassword, string ClientIp) : ICommand<Result>
{
    // Keeps the token and the password out of logs and exception messages.
    public override string ToString() => nameof(ResetPasswordCommand);
}

internal sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty().MaximumLength(AuthInputLimits.PasswordResetTokenMaxLength);
        RuleFor(c => c.NewPassword).MustBeNewPassword();
        RuleFor(c => c.ClientIp).NotEmpty().MaximumLength(RefreshToken.IpAddressMaxLength);
    }
}

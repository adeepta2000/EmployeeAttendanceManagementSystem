using Attendance.Application.Common.Messaging;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Identity.Logout;

/// <summary>
/// Ends the session the refresh token belongs to by revoking its whole family. The access token
/// stays valid until it expires (at most 15 minutes), so the client must discard it too.
/// </summary>
public sealed record LogoutCommand(string RefreshToken) : ICommand<Result>
{
    // Keeps the token out of logs and exception messages.
    public override string ToString() => nameof(LogoutCommand);
}

internal sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty().MaximumLength(AuthInputLimits.RefreshTokenMaxLength);
    }
}

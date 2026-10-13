using Attendance.Application.Common.Messaging;
using Attendance.Domain.Identity;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Identity.Login;

/// <summary>Signs in with email and password and starts a new session (spec §2.5).</summary>
/// <param name="ClientIp">The caller's IP address, recorded on the refresh token.</param>
/// <param name="UserAgent">The caller's user agent, recorded on the refresh token.</param>
public sealed record LoginCommand(string Email, string Password, string ClientIp, string? UserAgent)
    : ICommand<Result<AuthTokens>>
{
    // Keeps the password out of logs and exception messages.
    public override string ToString() => nameof(LoginCommand);
}

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(User.EmailMaxLength).EmailAddress();
        RuleFor(c => c.Password).NotEmpty().MaximumLength(AuthInputLimits.PasswordMaxLength);
        RuleFor(c => c.ClientIp).NotEmpty().MaximumLength(RefreshToken.IpAddressMaxLength);
        RuleFor(c => c.UserAgent).MaximumLength(RefreshToken.UserAgentMaxLength);
    }
}

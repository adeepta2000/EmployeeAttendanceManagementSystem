using Attendance.Application.Common.Messaging;
using Attendance.Shared.Results;
using FluentValidation;
using RefreshTokenEntity = Attendance.Domain.Identity.RefreshToken;

namespace Attendance.Application.Identity.Refresh;

/// <summary>Exchanges a refresh token for a new token pair, rotating the refresh token (spec §2.5).</summary>
/// <param name="ClientIp">The caller's IP address, recorded on the new refresh token.</param>
/// <param name="UserAgent">The caller's user agent, recorded on the new refresh token.</param>
public sealed record RefreshTokenCommand(string RefreshToken, string ClientIp, string? UserAgent)
    : ICommand<Result<AuthTokens>>
{
    // Keeps the token out of logs and exception messages.
    public override string ToString() => nameof(RefreshTokenCommand);
}

internal sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty().MaximumLength(AuthInputLimits.RefreshTokenMaxLength);
        RuleFor(c => c.ClientIp).NotEmpty().MaximumLength(RefreshTokenEntity.IpAddressMaxLength);
        RuleFor(c => c.UserAgent).MaximumLength(RefreshTokenEntity.UserAgentMaxLength);
    }
}

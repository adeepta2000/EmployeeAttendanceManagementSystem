using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Domain.Identity;
using Attendance.Shared.Results;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Attendance.Application.Identity.ForgotPassword;

/// <summary>
/// Asks for a password-reset link (screen 2). It always succeeds: the answer is the same whether or not
/// the address belongs to an active account, and the link, if any, arrives by email.
/// </summary>
/// <param name="ClientIp">The caller's IP address, recorded on the reset link.</param>
public sealed record ForgotPasswordCommand(string Email, string ClientIp) : ICommand<Result>
{
    // Keeps the email address out of logs and exception messages.
    public override string ToString() => nameof(ForgotPasswordCommand);
}

internal sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(User.EmailMaxLength).EmailAddress();
        RuleFor(c => c.ClientIp).NotEmpty().MaximumLength(RefreshToken.IpAddressMaxLength);
    }
}

internal sealed class ForgotPasswordCommandHandler(IBackgroundWorkQueue backgroundWork, ILogger<ForgotPasswordCommandHandler> logger)
    : ICommandHandler<ForgotPasswordCommand, Result>
{
    public Task<Result> HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        // Looking the account up, storing a link and sending the email all happen after the response.
        // Otherwise the response time would differ between known and unknown addresses and reveal which
        // accounts exist (§2.6), however the response body is worded.
        var (email, clientIp) = (command.Email, command.ClientIp);
        var queued = backgroundWork.TryEnqueue(
            "password-reset-link",
            (services, ct) => services.GetRequiredService<PasswordResetLinkIssuer>().IssueAsync(email, clientIp, ct));

        if (!queued)
        {
            logger.LogWarning("Password reset request from {ClientIp} dropped: the background queue is full", clientIp);
        }

        return Task.FromResult(Result.Success());
    }
}

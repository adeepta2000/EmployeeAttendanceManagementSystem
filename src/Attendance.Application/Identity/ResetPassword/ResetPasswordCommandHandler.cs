using Attendance.Application.Common.Exceptions;
using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Identity.Abstractions;
using Attendance.Shared.Results;
using Microsoft.Extensions.Logging;

namespace Attendance.Application.Identity.ResetPassword;

internal sealed class ResetPasswordCommandHandler(
    IPasswordResetTokenRepository resetTokens,
    IPasswordResetTokenGenerator tokenGenerator,
    PasswordService passwords,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ResetPasswordCommandHandler> logger)
    : ICommandHandler<ResetPasswordCommand, Result>
{
    private const int MaxSaveAttempts = 3;

    public async Task<Result> HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ResetAsync(command, cancellationToken);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxSaveAttempts)
            {
                // Something we change moved under us: the same link submitted twice at once (UsedUtc is a
                // concurrency token), a sign-in attempt, or a session refresh. Start over from the
                // database; a link the other request used is then simply no longer usable.
                unitOfWork.DiscardChanges();
            }
        }
    }

    private async Task<Result> ResetAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var link = await resetTokens.FindByHashAsync(tokenGenerator.Hash(command.Token), cancellationToken);

        if (link is null)
        {
            logger.LogWarning("Password reset from {ClientIp} rejected: unknown token", command.ClientIp);
            return Result.Failure(AuthErrors.InvalidResetToken);
        }

        var user = link.User;
        if (!link.IsUsable(now) || !user.IsActive)
        {
            logger.LogWarning(
                "Password reset for user {UserId} from {ClientIp} rejected: link used {UsedUtc:o}, revoked {RevokedUtc:o}, expires {ExpiresUtc:o}, account active {IsActive}",
                user.PublicId, command.ClientIp, link.UsedUtc, link.RevokedUtc, link.ExpiresUtc, user.IsActive);
            return Result.Failure(AuthErrors.InvalidResetToken);
        }

        link.MarkUsed(now);
        var endedSessions = await passwords.ReplaceAsync(user, command.NewPassword, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} reset their password from {ClientIp}; ended {SessionCount} sessions",
            user.PublicId, command.ClientIp, endedSessions);
        return Result.Success();
    }
}

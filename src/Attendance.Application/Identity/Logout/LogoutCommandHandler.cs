using Attendance.Application.Common.Messaging;
using Attendance.Application.Identity.Abstractions;
using Attendance.Shared.Results;
using Microsoft.Extensions.Logging;

namespace Attendance.Application.Identity.Logout;

internal sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenGenerator refreshTokenGenerator,
    AuthSessionService sessions,
    TimeProvider timeProvider,
    ILogger<LogoutCommandHandler> logger)
    : ICommandHandler<LogoutCommand, Result>
{
    public async Task<Result> HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        var token = await refreshTokens.FindByHashAsync(
            refreshTokenGenerator.Hash(command.RefreshToken), cancellationToken);

        // An unknown token has no session to end. Succeed anyway: signing out is idempotent, and the
        // response shouldn't reveal whether a token exists.
        if (token is null)
        {
            return Result.Success();
        }

        // Any token of the family ends the session, even an already rotated one.
        await sessions.RevokeFamilyAsync(token.FamilyId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        logger.LogInformation("User {UserId} signed out of token family {FamilyId}", token.User.PublicId, token.FamilyId);
        return Result.Success();
    }
}

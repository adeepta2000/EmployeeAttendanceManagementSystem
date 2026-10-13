using Attendance.Application.Common.Exceptions;
using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Identity.Abstractions;
using Attendance.Shared.Results;
using Microsoft.Extensions.Logging;

namespace Attendance.Application.Identity.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenGenerator refreshTokenGenerator,
    AuthSessionService sessions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<RefreshTokenCommandHandler> logger)
    : ICommandHandler<RefreshTokenCommand, Result<AuthTokens>>
{
    public async Task<Result<AuthTokens>> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var presented = await refreshTokens.FindByHashAsync(
            refreshTokenGenerator.Hash(command.RefreshToken), cancellationToken);

        if (presented is null)
        {
            logger.LogWarning("Token refresh from {ClientIp} rejected: unknown refresh token", command.ClientIp);
            return AuthErrors.InvalidRefreshToken;
        }

        var user = presented.User;
        var familyId = presented.FamilyId;

        if (presented.IsRevoked)
        {
            // Reuse detection (spec §2.5). A token that was already rotated or revoked has come back, so
            // two parties hold copies of this session and one of them stole it. We can't tell which, so
            // end the session for both: revoke the whole family and make the user sign in again.
            await sessions.RevokeFamilyAsync(familyId, now, cancellationToken);
            LogReuse(user.PublicId, familyId, command.ClientIp);
            return AuthErrors.InvalidRefreshToken;
        }

        if (presented.IsExpired(now))
        {
            logger.LogInformation("Token refresh for user {UserId} rejected: refresh token expired", user.PublicId);
            return AuthErrors.InvalidRefreshToken;
        }

        if (!user.IsActive)
        {
            await sessions.RevokeFamilyAsync(familyId, now, cancellationToken);
            logger.LogWarning(
                "Token refresh for user {UserId} rejected: account inactive; revoked token family {FamilyId}",
                user.PublicId, familyId);
            return AuthErrors.InvalidRefreshToken;
        }

        // Rotation: the new token joins the same family, and the presented one is revoked and points at it.
        var session = await sessions.IssueAsync(
            user, familyId, command.ClientIp, command.UserAgent, now, cancellationToken);
        presented.Revoke(now, replacedByTokenHash: session.RefreshTokenHash);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request rotated this same token between our read and our write (RevokedUtc is a
            // concurrency token, so only one rotation can win). The token was presented twice, which is
            // reuse as well. Nothing of ours was saved.
            unitOfWork.DiscardChanges();
            await sessions.RevokeFamilyAsync(familyId, now, cancellationToken);
            LogReuse(user.PublicId, familyId, command.ClientIp);
            return AuthErrors.InvalidRefreshToken;
        }

        logger.LogInformation("User {UserId} refreshed token family {FamilyId}", user.PublicId, familyId);
        return Result<AuthTokens>.Success(session.Tokens);
    }

    private void LogReuse(Guid userId, Guid familyId, string clientIp) =>
        logger.LogWarning(
            "Refresh token reuse detected for user {UserId} from {ClientIp}: revoked token family {FamilyId}",
            userId, clientIp, familyId);
}

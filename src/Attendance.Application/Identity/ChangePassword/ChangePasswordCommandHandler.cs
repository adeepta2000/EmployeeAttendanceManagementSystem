using Attendance.Application.Common.Exceptions;
using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Identity.Abstractions;
using Attendance.Shared.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Attendance.Application.Identity.ChangePassword;

internal sealed class ChangePasswordCommandHandler(
    IUserContextService userContext,
    IUserRepository users,
    IPasswordHasher passwordHasher,
    PasswordService passwords,
    AuthSessionService sessions,
    IUnitOfWork unitOfWork,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider,
    ILogger<ChangePasswordCommandHandler> logger)
    : ICommandHandler<ChangePasswordCommand, Result<AuthTokens>>
{
    private const int MaxSaveAttempts = 3;

    public async Task<Result<AuthTokens>> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ChangeAsync(command, cancellationToken);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxSaveAttempts)
            {
                // A concurrent sign-in changed the failure count, or a concurrent refresh rotated one of the
                // sessions being ended, between our read and our write. Start over from the database.
                unitOfWork.DiscardChanges();
            }
        }
    }

    private async Task<Result<AuthTokens>> ChangeAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var caller = await userContext.GetCurrentAsync(cancellationToken);
        var user = await users.FindByIdAsync(caller.UserId, cancellationToken) ?? throw new UnauthenticatedException();
        var policy = options.Value;

        // A stolen access token must not become a way to guess the password: wrong current passwords
        // count towards the same lockout as failed sign-ins.
        if (user.IsLockedOut(now))
        {
            logger.LogWarning(
                "Password change for user {UserId} from {ClientIp} rejected: locked out until {LockoutEndUtc:o}",
                user.PublicId, command.ClientIp, user.LockoutEndUtc);
            return AuthErrors.LockedOut(policy.LockoutDuration);
        }

        if (passwordHasher.Verify(user.PasswordHash, command.CurrentPassword) == PasswordCheck.Failed)
        {
            var lockedOut = user.RegisterFailedLogin(now, policy.MaxFailedLoginAttempts, policy.LockoutDuration);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Password change for user {UserId} from {ClientIp} rejected: wrong current password ({FailedLoginCount} of {MaxFailedLoginAttempts})",
                user.PublicId, command.ClientIp, user.FailedLoginCount, policy.MaxFailedLoginAttempts);
            return lockedOut ? AuthErrors.LockedOut(policy.LockoutDuration) : AuthErrors.InvalidCurrentPassword;
        }

        var endedSessions = await passwords.ReplaceAsync(user, command.NewPassword, now, cancellationToken);
        var session = await sessions.IssueAsync(
            user, familyId: Guid.NewGuid(), command.ClientIp, command.UserAgent, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} changed their password from {ClientIp}; ended {SessionCount} sessions",
            user.PublicId, command.ClientIp, endedSessions);
        return Result<AuthTokens>.Success(session.Tokens);
    }
}

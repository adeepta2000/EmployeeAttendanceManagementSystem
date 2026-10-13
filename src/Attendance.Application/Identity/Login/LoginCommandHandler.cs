using Attendance.Application.Common.Exceptions;
using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Identity.Abstractions;
using Attendance.Shared.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Attendance.Application.Identity.Login;

internal sealed class LoginCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    AuthSessionService sessions,
    IUnitOfWork unitOfWork,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider,
    ILogger<LoginCommandHandler> logger)
    : ICommandHandler<LoginCommand, Result<AuthTokens>>
{
    private const int MaxSaveAttempts = 3;

    public async Task<Result<AuthTokens>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await SignInAsync(command, cancellationToken);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxSaveAttempts)
            {
                // A concurrent sign-in to the same account changed its failure count first
                // (FailedLoginCount is a concurrency token). Start over from the database so that
                // parallel guesses can't slip past the lockout uncounted.
                unitOfWork.DiscardChanges();
            }
        }
    }

    private async Task<Result<AuthTokens>> SignInAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = await users.FindByEmailAsync(command.Email, cancellationToken);

        // Every rejection below costs one password-hash check and returns the same error, so neither
        // the response nor its timing reveals whether the account exists, is inactive or is locked.
        if (user is null || !user.IsActive)
        {
            passwordHasher.SimulateVerify(command.Password);
            logger.LogWarning("Sign-in from {ClientIp} rejected: unknown or inactive account", command.ClientIp);
            return AuthErrors.InvalidCredentials;
        }

        if (user.IsLockedOut(now))
        {
            passwordHasher.SimulateVerify(command.Password);
            logger.LogWarning(
                "Sign-in for user {UserId} from {ClientIp} rejected: locked out until {LockoutEndUtc:o}",
                user.PublicId, command.ClientIp, user.LockoutEndUtc);
            return AuthErrors.InvalidCredentials;
        }

        var check = passwordHasher.Verify(user.PasswordHash, command.Password);
        if (check == PasswordCheck.Failed)
        {
            var policy = options.Value;
            var lockedOut = user.RegisterFailedLogin(now, policy.MaxFailedLoginAttempts, policy.LockoutDuration);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (lockedOut)
            {
                logger.LogWarning(
                    "User {UserId} locked out until {LockoutEndUtc:o} after {FailedLoginCount} failed sign-ins, the last from {ClientIp}",
                    user.PublicId, user.LockoutEndUtc, user.FailedLoginCount, command.ClientIp);
            }
            else
            {
                logger.LogWarning(
                    "Sign-in for user {UserId} from {ClientIp} rejected: wrong password ({FailedLoginCount} of {MaxFailedLoginAttempts})",
                    user.PublicId, command.ClientIp, user.FailedLoginCount, policy.MaxFailedLoginAttempts);
            }

            return AuthErrors.InvalidCredentials;
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.Hash(command.Password);
        }

        user.RegisterSuccessfulLogin(now);
        var session = await sessions.IssueAsync(
            user, familyId: Guid.NewGuid(), command.ClientIp, command.UserAgent, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} signed in from {ClientIp}", user.PublicId, command.ClientIp);
        return Result<AuthTokens>.Success(session.Tokens);
    }
}

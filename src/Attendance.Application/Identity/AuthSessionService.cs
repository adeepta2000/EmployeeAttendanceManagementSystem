using Attendance.Application.Common.Exceptions;
using Attendance.Application.Common.Interfaces;
using Attendance.Application.Identity.Abstractions;
using Attendance.Domain.Identity;
using Microsoft.Extensions.Options;

namespace Attendance.Application.Identity;

/// <summary>
/// Issues and ends sign-in sessions. A session is one refresh-token family: sign-in starts it, each
/// refresh rotates it to a new token in the same family, and revoking the family ends it.
/// </summary>
internal sealed class AuthSessionService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    IUnitOfWork unitOfWork,
    IOptions<AuthOptions> options)
{
    private const int MaxRevokeAttempts = 3;

    /// <summary>
    /// Creates an access token with the user's current permissions and a new refresh token in
    /// <paramref name="familyId"/>. The refresh token is added to the unit of work but not saved.
    /// </summary>
    public async Task<IssuedSession> IssueAsync(
        User user,
        Guid familyId,
        string clientIp,
        string? userAgent,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var permissions = await users.GetPermissionCodesAsync(user.Id, cancellationToken);
        var accessToken = accessTokenIssuer.Issue(
            new AccessTokenSubject(user.PublicId, user.Employee?.PublicId, permissions, user.MustChangePassword), nowUtc);

        var refreshToken = refreshTokenGenerator.Generate();
        var stored = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshToken.Hash,
            FamilyId = familyId,
            ExpiresUtc = nowUtc + options.Value.RefreshTokenLifetime,
            CreatedByIp = clientIp,
            UserAgent = userAgent,
        };
        refreshTokens.Add(stored);

        var tokens = new AuthTokens(
            accessToken.Token, accessToken.ExpiresUtc, refreshToken.Token, stored.ExpiresUtc, user.MustChangePassword);
        return new IssuedSession(tokens, stored.TokenHash);
    }

    /// <summary>Revokes every token of the family that isn't revoked yet, and saves.</summary>
    public async Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            foreach (var token in await refreshTokens.GetUnrevokedInFamilyAsync(familyId, cancellationToken))
            {
                token.Revoke(nowUtc);
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (ConcurrencyConflictException) when (attempt < MaxRevokeAttempts)
            {
                // A concurrent refresh rotated one of these tokens after we read it. Revocation only moves
                // forward, so read the family again (now including the new token) and finish the job.
                unitOfWork.DiscardChanges();
            }
        }
    }
}

/// <param name="RefreshTokenHash">The stored hash of the new refresh token, for linking its predecessor to it.</param>
internal sealed record IssuedSession(AuthTokens Tokens, string RefreshTokenHash);

public static class AuthInputLimits
{
    /// <summary>Caps the work a caller can force on the password hasher.</summary>
    public const int PasswordMaxLength = 256;

    /// <summary>
    /// The shortest new password accepted. Length is the whole policy: no composition rules, which
    /// push people towards predictable patterns (NIST SP 800-63B).
    /// </summary>
    public const int NewPasswordMinLength = 12;

    /// <summary>Issued refresh tokens are 86 characters; anything much longer isn't one of ours.</summary>
    public const int RefreshTokenMaxLength = 256;

    /// <summary>Issued reset tokens are 43 characters; anything much longer isn't one of ours.</summary>
    public const int PasswordResetTokenMaxLength = 256;
}

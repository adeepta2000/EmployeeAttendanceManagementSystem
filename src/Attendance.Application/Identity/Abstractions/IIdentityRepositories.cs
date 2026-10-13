using Attendance.Domain.Identity;

namespace Attendance.Application.Identity.Abstractions;

public interface IUserRepository
{
    /// <summary>
    /// Finds a user by email (normalized here, as stored), with their employee loaded. The user is
    /// tracked: changes are saved by <c>IUnitOfWork</c>.
    /// </summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>The distinct permission codes granted to the user through all of their roles.</summary>
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(long userId, CancellationToken cancellationToken);
}

public interface IRefreshTokenRepository
{
    /// <summary>Finds a token by hash in any state, with its user and the user's employee loaded. Tracked.</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Every token of the family that is not yet revoked, expired ones included. Tracked.</summary>
    Task<IReadOnlyList<RefreshToken>> GetUnrevokedInFamilyAsync(Guid familyId, CancellationToken cancellationToken);

    void Add(RefreshToken token);
}

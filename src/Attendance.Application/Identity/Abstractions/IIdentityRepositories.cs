using Attendance.Domain.Identity;

namespace Attendance.Application.Identity.Abstractions;

public interface IUserRepository
{
    /// <summary>
    /// Finds a user by email (normalized here, as stored), with their employee loaded. The user is
    /// tracked: changes are saved by <c>IUnitOfWork</c>.
    /// </summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Finds a user by internal ID, with their employee loaded. Tracked.</summary>
    Task<User?> FindByIdAsync(long userId, CancellationToken cancellationToken);

    /// <summary>The login account linked to an employee, if they have one (FR-1.3). Tracked.</summary>
    Task<User?> FindByEmployeeIdAsync(long employeeId, CancellationToken cancellationToken);

    /// <summary>The distinct permission codes granted to the user through all of their roles.</summary>
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(long userId, CancellationToken cancellationToken);
}

public interface IRefreshTokenRepository
{
    /// <summary>Finds a token by hash in any state, with its user and the user's employee loaded. Tracked.</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Every token of the family that is not yet revoked, expired ones included. Tracked.</summary>
    Task<IReadOnlyList<RefreshToken>> GetUnrevokedInFamilyAsync(Guid familyId, CancellationToken cancellationToken);

    /// <summary>Every token of the user, in any family, that is not yet revoked. Tracked.</summary>
    Task<IReadOnlyList<RefreshToken>> GetUnrevokedForUserAsync(long userId, CancellationToken cancellationToken);

    void Add(RefreshToken token);
}

public interface IPasswordResetTokenRepository
{
    /// <summary>Finds a reset link by token hash in any state, with its user and the user's employee loaded. Tracked.</summary>
    Task<PasswordResetToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>The user's links that are neither used nor revoked, expired ones included. Tracked.</summary>
    Task<IReadOnlyList<PasswordResetToken>> GetOutstandingForUserAsync(long userId, CancellationToken cancellationToken);

    void Add(PasswordResetToken token);
}

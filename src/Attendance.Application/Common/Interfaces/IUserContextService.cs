using Attendance.Application.Common.Exceptions;

namespace Attendance.Application.Common.Interfaces;

/// <summary>
/// Resolves who the caller is, for authorization and data scoping in the query layer (spec §2.5,
/// FR-8.3). Scoped to one request; each lookup is cached for the rest of it.
/// </summary>
public interface IUserContextService
{
    /// <summary>The caller's internal IDs and permissions.</summary>
    /// <exception cref="UnauthenticatedException">Nobody is signed in, or the account is no longer active.</exception>
    Task<UserContext> GetCurrentAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Internal IDs of every employee who reports to the caller, directly or indirectly. The caller is
    /// not included. Inactive employees are, so their history stays in their manager's scope (FR-1.5).
    /// Empty when the caller has no employee record or no reports.
    /// </summary>
    /// <exception cref="UnauthenticatedException">Nobody is signed in, or the account is no longer active.</exception>
    Task<IReadOnlySet<long>> GetReportingTreeAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The signed-in caller. The internal IDs are for joins and filters only and must never reach a
/// response (CLAUDE.md data conventions).
/// </summary>
/// <param name="EmployeeId">Null for an account with no employee record, such as an IT admin.</param>
/// <param name="Permissions">The permission codes from the caller's access token.</param>
public sealed record UserContext(long UserId, Guid UserPublicId, long? EmployeeId, IReadOnlySet<string> Permissions)
{
    public bool HasPermission(string permissionCode) => Permissions.Contains(permissionCode);
}

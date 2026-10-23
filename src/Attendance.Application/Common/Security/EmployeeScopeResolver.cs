using Attendance.Application.Common.Exceptions;
using Attendance.Application.Common.Interfaces;
using Attendance.Domain.Identity;

namespace Attendance.Application.Common.Security;

/// <summary>
/// Works out the caller's <see cref="EmployeeScope"/> (FR-8.3). Handlers call it and pass the result to
/// the repository; controllers never see it.
/// </summary>
/// <remarks>
/// The rule is permission-based, never role-based: <c>scope.company</c> sees everyone; anyone else sees
/// themselves plus their reporting tree. A plain employee has no reports, so that is just themselves;
/// a manager gets their whole tree, inactive reports included (FR-1.5).
/// </remarks>
internal sealed class EmployeeScopeResolver(IUserContextService userContext)
{
    /// <exception cref="UnauthenticatedException">Nobody is signed in, or the account is no longer active.</exception>
    public async Task<EmployeeScope> GetEmployeeScopeAsync(CancellationToken cancellationToken)
    {
        var current = await userContext.GetCurrentAsync(cancellationToken);
        if (current.HasPermission(Permissions.Scope.Company))
        {
            return EmployeeScope.Company;
        }

        var tree = await userContext.GetReportingTreeAsync(cancellationToken);
        return current.EmployeeId is { } self
            ? EmployeeScope.Restricted(tree.Append(self))
            : EmployeeScope.Restricted(tree);
    }
}

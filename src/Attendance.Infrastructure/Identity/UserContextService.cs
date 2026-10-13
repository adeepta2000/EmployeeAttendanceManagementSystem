using Attendance.Application.Common.Exceptions;
using Attendance.Application.Common.Interfaces;
using Attendance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Identity;

internal sealed class UserContextService(ICurrentUser currentUser, AttendanceDbContext db) : IUserContextService
{
    private UserContext? _current;
    private IReadOnlySet<long>? _reportingTree;

    public async Task<UserContext> GetCurrentAsync(CancellationToken cancellationToken)
    {
        if (_current is not null)
        {
            return _current;
        }

        if (!Guid.TryParse(currentUser.UserId, out var userPublicId))
        {
            throw new UnauthenticatedException();
        }

        // The token outlives a deactivation by up to 15 minutes; checking IsActive here closes that gap.
        var user = await db.Users.AsNoTracking()
            .Where(u => u.PublicId == userPublicId && u.IsActive)
            .Select(u => new { u.Id, u.EmployeeId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthenticatedException();

        _current = new UserContext(
            user.Id, userPublicId, user.EmployeeId, currentUser.Permissions.ToHashSet(StringComparer.Ordinal));
        return _current;
    }

    public async Task<IReadOnlySet<long>> GetReportingTreeAsync(CancellationToken cancellationToken)
    {
        if (_reportingTree is not null)
        {
            return _reportingTree;
        }

        var current = await GetCurrentAsync(cancellationToken);
        var tree = new HashSet<long>();

        // Breadth-first, one query per management level. The visited set ends the walk even if bad
        // data puts someone in their own reporting chain.
        if (current.EmployeeId is { } self)
        {
            var managers = new List<long> { self };
            while (managers.Count > 0)
            {
                var reports = await db.Employees.AsNoTracking()
                    .Where(e => e.ReportingManagerId.HasValue && managers.Contains(e.ReportingManagerId.Value))
                    .Select(e => e.Id)
                    .ToListAsync(cancellationToken);

                managers = reports.Where(id => id != self && tree.Add(id)).ToList();
            }
        }

        _reportingTree = tree;
        return tree;
    }
}

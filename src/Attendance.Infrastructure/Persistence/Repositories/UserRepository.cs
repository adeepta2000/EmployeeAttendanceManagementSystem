using Attendance.Application.Identity.Abstractions;
using Attendance.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AttendanceDbContext db) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = User.NormalizeEmail(email);
        return db.Users
            .Include(u => u.Employee)
            .SingleOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(long userId, CancellationToken cancellationToken) =>
        await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code))
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);
}

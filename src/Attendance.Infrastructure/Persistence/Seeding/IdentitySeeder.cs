using Attendance.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Attendance.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the permission catalog and the system roles. It runs on every startup and is idempotent.
/// </summary>
/// <remarks>
/// Once a role exists, its grants belong to the admins who edit it (screen 33), so the seeder never
/// re-adds a grant that was removed and never removes one. It grants a role's default permissions
/// only when the role is first created, or when a permission is new to the catalog.
/// </remarks>
public sealed class IdentitySeeder(AttendanceDbContext db, ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var permissions = await db.Permissions.ToDictionaryAsync(p => p.Code, StringComparer.Ordinal, cancellationToken);
        var newPermissions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in Permissions.All)
        {
            if (permissions.TryGetValue(definition.Code, out var existing))
            {
                // EF writes these only if they actually changed.
                existing.Module = definition.Module;
                existing.Description = definition.Description;
                continue;
            }

            var permission = new Permission
            {
                Code = definition.Code,
                Module = definition.Module,
                Description = definition.Description,
            };
            db.Permissions.Add(permission);
            permissions.Add(permission.Code, permission);
            newPermissions.Add(permission.Code);
        }

        var roles = await db.Roles.ToDictionaryAsync(r => r.Name, StringComparer.Ordinal, cancellationToken);
        var newRoles = 0;
        var newGrants = 0;

        foreach (var systemRole in SystemRoles.All)
        {
            var isNewRole = !roles.TryGetValue(systemRole.Name, out var role);
            if (role is null)
            {
                role = new Role { Name = systemRole.Name, Description = systemRole.Description, IsSystem = true };
                db.Roles.Add(role);
                newRoles++;
            }

            foreach (var code in systemRole.DefaultPermissions.Where(code => isNewRole || newPermissions.Contains(code)))
            {
                role.RolePermissions.Add(new RolePermission { Role = role, Permission = permissions[code] });
                newGrants++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (newPermissions.Count > 0 || newRoles > 0)
        {
            logger.LogInformation(
                "Seeded {PermissionCount} permissions, {RoleCount} roles and {GrantCount} role grants",
                newPermissions.Count, newRoles, newGrants);
        }
    }
}

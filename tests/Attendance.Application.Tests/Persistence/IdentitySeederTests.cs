using Attendance.Domain.Identity;
using Attendance.Infrastructure.Persistence.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Attendance.Application.Tests.Persistence;

public sealed class IdentitySeederTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seeds_the_whole_permission_catalog()
    {
        await SeedAsync();

        await using var context = _db.CreateContext();
        var seeded = await context.Permissions.Select(p => new { p.Code, p.Module, p.Description }).ToListAsync(Ct);
        seeded.Should().BeEquivalentTo(Permissions.All.Select(p => new { p.Code, p.Module, p.Description }));
    }

    [Fact]
    public async Task Seeds_the_FR_8_1_roles_as_system_roles_with_their_default_permissions()
    {
        await SeedAsync();

        var grants = await GetGrantsAsync();
        grants.Keys.Should().BeEquivalentTo(["Admin", "HRManager", "HROfficer", "Manager", "Employee"]);
        foreach (var role in SystemRoles.All)
        {
            grants[role.Name].Should().BeEquivalentTo(role.DefaultPermissions, role.Name);
        }

        await using var context = _db.CreateContext();
        (await context.Roles.AllAsync(r => r.IsSystem, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Only_Admin_and_HRManager_can_read_the_audit_log() =>
        (await RolesWithAsync(Permissions.Audit.Read)).Should().BeEquivalentTo(["Admin", "HRManager"], "FR-12.2");

    [Fact]
    public async Task Only_HRManager_can_unlock_a_period() =>
        (await RolesWithAsync(Permissions.Period.Unlock)).Should().BeEquivalentTo(["HRManager"], "FR-10.2");

    [Fact]
    public async Task Seeding_twice_changes_nothing()
    {
        await SeedAsync();
        var first = await GetGrantsAsync();

        await SeedAsync();

        (await GetGrantsAsync()).Should().BeEquivalentTo(first);
        await using var context = _db.CreateContext();
        (await context.Permissions.CountAsync(Ct)).Should().Be(Permissions.All.Count);
    }

    [Fact]
    public async Task A_grant_an_admin_removed_stays_removed()
    {
        await SeedAsync();
        await using (var context = _db.CreateContext())
        {
            var grant = await context.RolePermissions
                .SingleAsync(rp => rp.Role.Name == SystemRoles.Manager && rp.Permission.Code == Permissions.Report.View, Ct);
            context.RolePermissions.Remove(grant);
            await context.SaveChangesAsync(Ct);
        }

        await SeedAsync();

        (await GetGrantsAsync())[SystemRoles.Manager].Should().NotContain(Permissions.Report.View);
    }

    private async Task SeedAsync()
    {
        await using var context = _db.CreateContext();
        await new IdentitySeeder(context, NullLogger<IdentitySeeder>.Instance).SeedAsync(Ct);
    }

    private async Task<Dictionary<string, List<string>>> GetGrantsAsync()
    {
        await using var context = _db.CreateContext();
        var roles = await context.Roles
            .Select(r => new { r.Name, Codes = r.RolePermissions.Select(rp => rp.Permission.Code).ToList() })
            .ToListAsync(Ct);
        return roles.ToDictionary(r => r.Name, r => r.Codes);
    }

    private async Task<IEnumerable<string>> RolesWithAsync(string permissionCode)
    {
        await SeedAsync();
        return (await GetGrantsAsync()).Where(g => g.Value.Contains(permissionCode)).Select(g => g.Key);
    }
}

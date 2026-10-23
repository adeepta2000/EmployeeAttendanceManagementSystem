using Attendance.Application.Identity.Abstractions;
using Attendance.Application.Tests.Persistence;
using Attendance.Domain.Organization;
using Attendance.Infrastructure.Identity;
using Attendance.Infrastructure.Persistence.Seeding;
using Attendance.Infrastructure.Persistence.Seeding.Demo;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Attendance.Application.Tests.Organization;

public sealed class DemoDataSeederTests : IDisposable
{
    private const string Password = "Demo-Pa55word-123";

    private readonly SqliteTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seeds_the_company_with_a_login_for_everyone_and_an_admin_without_an_employee_record()
    {
        await SeedAsync(enabled: true);

        await using var context = _db.CreateContext();
        (await context.Employees.CountAsync(Ct)).Should().Be(120);
        (await context.Users.CountAsync(Ct)).Should().Be(121);
        (await context.Departments.CountAsync(d => d.HeadEmployeeId != null, Ct)).Should().Be(6);

        var admin = await context.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .SingleAsync(u => u.Email == DemoDataSeeder.AdminEmail, Ct);
        admin.EmployeeId.Should().BeNull();
        admin.UserRoles.Should().ContainSingle().Which.Role.Name.Should().Be(SystemRoles.Admin);

        // Leavers' accounts are inactive, as deactivation through the API would leave them.
        var leaverAccounts = await context.Users.Where(u => u.Employee != null && !u.Employee.IsActive).ToListAsync(Ct);
        leaverAccounts.Should().HaveCount(3).And.OnlyContain(u => !u.IsActive);
    }

    [Fact]
    public async Task Every_account_signs_in_with_the_configured_password()
    {
        await SeedAsync(enabled: true);

        await using var context = _db.CreateContext();
        var hasher = new IdentityPasswordHasher();
        var hashes = await context.Users.Select(u => u.PasswordHash).Distinct().ToListAsync(Ct);
        hashes.Should().ContainSingle();
        hasher.Verify(hashes[0], Password).Should().NotBe(PasswordCheck.Failed);
    }

    [Fact]
    public async Task Does_nothing_once_organization_data_exists()
    {
        await SeedAsync(enabled: true);
        await using (var context = _db.CreateContext())
        {
            context.Departments.Add(new Department { Code = "KNT", Name = "Knitting" });
            await context.SaveChangesAsync(Ct);
        }

        await SeedAsync(enabled: true);

        await using var check = _db.CreateContext();
        (await check.Employees.CountAsync(Ct)).Should().Be(120);
        (await check.Departments.CountAsync(Ct)).Should().Be(7);
    }

    [Fact]
    public async Task Does_nothing_when_disabled()
    {
        await SeedAsync(enabled: false);

        await using var context = _db.CreateContext();
        (await context.Employees.AnyAsync(Ct)).Should().BeFalse();
    }

    [Fact]
    public void Enabled_without_a_password_fails_with_instructions()
    {
        var services = new ServiceCollection();
        services.AddOptions<DemoDataOptions>()
            .Configure(o => o.Enabled = true)
            .Validate(o => o.IsValid, DemoDataOptions.ValidationMessage);

        var act = () => services.BuildServiceProvider().GetRequiredService<IOptions<DemoDataOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().WithMessage("*dotnet user-secrets set \"DemoData:Password\"*");
    }

    private async Task SeedAsync(bool enabled)
    {
        await using (var context = _db.CreateContext())
        {
            await new IdentitySeeder(context, NullLogger<IdentitySeeder>.Instance).SeedAsync(Ct);
        }

        await using (var context = _db.CreateContext())
        {
            var options = Options.Create(new DemoDataOptions { Enabled = enabled, Password = Password });
            await new DemoDataSeeder(context, new IdentityPasswordHasher(), options, _db.Clock, NullLogger<DemoDataSeeder>.Instance)
                .SeedAsync(Ct);
        }
    }
}

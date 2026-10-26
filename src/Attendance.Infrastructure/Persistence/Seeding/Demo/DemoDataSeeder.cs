using Attendance.Application.Identity;
using Attendance.Application.Identity.Abstractions;
using Attendance.Domain.Identity;
using Attendance.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Attendance.Infrastructure.Persistence.Seeding.Demo;

/// <summary>Bound from the <c>DemoData</c> section.</summary>
public sealed class DemoDataOptions
{
    public const string SectionName = "DemoData";

    /// <summary>Seed the demo company into an empty database at startup. On in Development only.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The password of every demo account. It is a secret like any other: set it with user-secrets or the
    /// <c>DemoData__Password</c> environment variable, never in a committed appsettings file.
    /// </summary>
    public string? Password { get; set; }

    // The same floor as any new password: demo accounts sign in with it.
    internal const int PasswordMinLength = AuthInputLimits.NewPasswordMinLength;

    internal bool IsValid => !Enabled || Password is { Length: >= PasswordMinLength };

    internal const string ValidationMessage =
        "DemoData:Password must be set (at least 12 characters) when DemoData:Enabled is true. Run: "
        + "dotnet user-secrets set \"DemoData:Password\" \"<password>\" --project src/Attendance.Api";
}

/// <summary>
/// Loads <see cref="NordwinDemoData"/> into an empty database: the org structure, 120 employees and a
/// login for each, plus an IT admin account with no employee record. It runs at startup after the
/// identity seed and does nothing once any organization data exists, so it never touches real data.
/// </summary>
public sealed class DemoDataSeeder(
    AttendanceDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<DemoDataOptions> options,
    TimeProvider timeProvider,
    ILogger<DemoDataSeeder> logger)
{
    public const string AdminEmail = "admin@" + NordwinDemoData.EmailDomain;

    private static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        if (await db.Departments.AnyAsync(cancellationToken) || await db.Employees.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Demo data not seeded: the database already has organization data");
            return;
        }

        var asOf = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Dhaka).DateTime);
        var data = NordwinDemoData.Generate(asOf);
        var roles = await db.Roles.ToDictionaryAsync(r => r.Name, StringComparer.Ordinal, cancellationToken);

        // One hash shared by every account: they all have the same password anyway, and hashing it 121
        // times with PBKDF2 would add seconds to every fresh start.
        var passwordHash = passwordHasher.Hash(settings.Password!);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.Departments.AddRange(data.Departments);
        db.Designations.AddRange(data.Designations);
        db.WorkLocations.AddRange(data.WorkLocations);
        foreach (var (employee, role) in data.Employees)
        {
            db.Employees.Add(employee);
            db.Users.Add(NewUser(employee.Email!, passwordHash, roles[role], employee));
        }

        db.Users.Add(NewUser(AdminEmail, passwordHash, roles[SystemRoles.Admin], employee: null));
        await db.SaveChangesAsync(cancellationToken);

        // Departments and employees point at each other, so the heads go in once both exist.
        foreach (var (department, head) in data.DepartmentHeads)
        {
            department.HeadEmployee = head;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Seeded demo data as of {AsOf}: {Departments} departments, {Designations} designations, {WorkLocations} work locations, {Employees} employees and {Users} accounts",
            asOf, data.Departments.Count, data.Designations.Count, data.WorkLocations.Count, data.Employees.Count, data.Employees.Count + 1);
    }

    private static User NewUser(string email, string passwordHash, Role role, Employee? employee)
    {
        var user = new User
        {
            Email = email,
            PasswordHash = passwordHash,
            Employee = employee,

            // A leaver's account goes with them, as deactivating an employee through the API does.
            IsActive = employee?.IsActive ?? true,
        };
        user.UserRoles.Add(new UserRole { Role = role });
        return user;
    }
}

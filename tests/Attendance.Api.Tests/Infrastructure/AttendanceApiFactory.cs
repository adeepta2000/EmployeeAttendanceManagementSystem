using Attendance.Application.Common.Events;
using Attendance.Application.Common.Interfaces;
using Attendance.Application.Identity.Abstractions;
using Attendance.Domain.Identity;
using Attendance.Domain.Organization;
using Attendance.Domain.Processing;
using Attendance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Attendance.Api.Tests.Infrastructure;

/// <summary>
/// The real API over a real SQL Server database. Starting it runs the startup guard, so the tests
/// also prove that the migrations apply and the seed data loads.
/// </summary>
public class AttendanceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Nordwin-Test-Pa55word";

    /// <summary>The client address that links in emails point at.</summary>
    public const string ClientBaseUrl = "https://attendance.nordwin.example";

    private static int _lastClientIp;
    private static int _lastCode;

    private readonly TestSqlServer _sqlServer = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Every email the app sent, instead of delivering it.</summary>
    public CapturingEmailSender Emails { get; } = new();

    /// <summary>Every attendance recalculation the app requested.</summary>
    public CapturingRecalculationHandler Recalculations { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _sqlServer.StartAsync(Ct);
        _ = Server;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Attendance", _sqlServer.ConnectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-of-at-least-32-chars");
        builder.UseSetting("Auth:ClientBaseUrl", ClientBaseUrl);
        builder.UseSetting("Serilog:MinimumLevel:Default", "Error");
        ConfigureSettings(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestClientIpStartupFilter>();
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.AddSingleton<IDomainEventHandler<AttendanceRecalculationRequested>>(Recalculations);
        });
    }

    /// <summary>Extra settings for a factory variant.</summary>
    protected virtual void ConfigureSettings(IWebHostBuilder builder)
    {
    }

    /// <summary>A client whose requests come from their own IP, so tests never share a rate-limit bucket.</summary>
    public HttpClient CreateApiClient(string? clientIp = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add(TestClientIpStartupFilter.HeaderName, clientIp ?? NextClientIp());
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Attendance.Api.Tests/1.0");
        return client;
    }

    public static string NextClientIp()
    {
        var n = Interlocked.Increment(ref _lastClientIp);
        return $"10.{(n >> 16) & 255}.{(n >> 8) & 255}.{n & 255}";
    }

    /// <summary>A code no other test uses (every test shares one database), upper-case as the API stores codes.</summary>
    public static string UniqueCode(string prefix) =>
        $"{prefix}-{Interlocked.Increment(ref _lastCode)}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

    /// <summary>
    /// Adds a user with <see cref="Password"/>, optionally in one of the seeded system roles and linked to
    /// an employee.
    /// </summary>
    public async Task<TestUser> CreateUserAsync(
        string? role = null,
        TestEmployee? employee = null,
        bool mustChangePassword = false,
        bool isActive = true)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            Email = $"user.{Guid.NewGuid():N}@nordwin.example",
            PasswordHash = passwordHasher.Hash(Password),
            EmployeeId = employee?.Id,
            MustChangePassword = mustChangePassword,
            IsActive = isActive,
        };
        if (role is not null)
        {
            user.UserRoles.Add(new UserRole { Role = await db.Roles.SingleAsync(r => r.Name == role, Ct) });
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return new TestUser(user.Email, user.PublicId);
    }

    /// <summary>Adds a custom (non-system) role granting exactly these permissions; returns its name.</summary>
    public async Task<string> CreateRoleAsync(params string[] permissionCodes)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();

        var role = new Role { Name = UniqueCode("role") };
        foreach (var permission in await db.Permissions.Where(p => permissionCodes.Contains(p.Code)).ToListAsync(Ct))
        {
            role.RolePermissions.Add(new RolePermission { Role = role, Permission = permission });
        }

        db.Roles.Add(role);
        await db.SaveChangesAsync(Ct);
        return role.Name;
    }

    /// <summary>A department, designation and work location of its own, so tests don't see each other's employees.</summary>
    public async Task<TestOrg> CreateOrgAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();

        var department = new Department { Code = UniqueCode("D"), Name = "Test department" };
        var designation = new Designation { Code = UniqueCode("G"), Name = "Test designation" };
        var location = new WorkLocation { Code = UniqueCode("L"), Name = "Test location" };
        db.AddRange(department, designation, location);
        await db.SaveChangesAsync(Ct);

        return new TestOrg(
            new TestRef(department.Id, department.PublicId, department.Code),
            new TestRef(designation.Id, designation.PublicId, designation.Code),
            new TestRef(location.Id, location.PublicId, location.Code));
    }

    /// <summary>Adds an employee straight to the database (no API, no scoping), with no login account.</summary>
    public async Task<TestEmployee> CreateEmployeeAsync(
        TestOrg org,
        TestEmployee? manager = null,
        string firstName = "Test",
        string lastName = "Employee",
        bool isActive = true,
        EmploymentType employmentType = EmploymentType.Permanent,
        DateOnly? joiningDate = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();

        var employee = new Employee
        {
            EmployeeCode = UniqueCode("E"),
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = new DateOnly(1990, 5, 17),
            Gender = Gender.Female,
            JoiningDate = joiningDate ?? new DateOnly(2020, 1, 1),
            EmploymentType = employmentType,
            IsActive = isActive,
            SeparationDate = isActive ? null : new DateOnly(2026, 6, 30),
            DepartmentId = org.Department.Id,
            DesignationId = org.Designation.Id,
            WorkLocationId = org.WorkLocation.Id,
            ReportingManagerId = manager?.Id,
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync(Ct);
        return new TestEmployee(employee.Id, employee.PublicId, employee.EmployeeCode);
    }

    /// <summary>Signs in a new user and returns a client that sends their access token.</summary>
    public async Task<ApiSession> SignInAsync(string role, TestEmployee? employee = null)
    {
        var user = await CreateUserAsync(role, employee);
        var client = CreateApiClient();
        var tokens = await client.SignInAsync(user);
        return new ApiSession(client, tokens.AccessToken, user);
    }

    /// <summary>
    /// Waits until the background queue has run everything queued so far, e.g. the email of a reset
    /// request. The queue runs one item at a time, so once a marker queued now has run, so has the rest.
    /// </summary>
    public async Task DrainBackgroundWorkAsync()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Services.GetRequiredService<IBackgroundWorkQueue>()
            .TryEnqueue("test-drain", (_, _) =>
            {
                done.SetResult();
                return Task.CompletedTask;
            })
            .Should().BeTrue();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
    }

    /// <summary>Reads the database directly, in its own scope, to check what the API stored.</summary>
    public async Task<T> QueryDbAsync<T>(Func<AttendanceDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AttendanceDbContext>());
    }
}

public sealed record TestUser(string Email, Guid PublicId);

/// <summary>A row created by a test: its internal Id for database setup, PublicId for API calls.</summary>
public sealed record TestRef(long Id, Guid PublicId, string Code);

public sealed record TestOrg(TestRef Department, TestRef Designation, TestRef WorkLocation);

public sealed record TestEmployee(long Id, Guid PublicId, string EmployeeCode);

/// <summary>Every API test class joins this collection: one app and one database, tests run one at a time.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<AttendanceApiFactory>
{
    public const string Name = "Api";
}

using Attendance.Application.Identity.Abstractions;
using Attendance.Domain.Identity;
using Attendance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Attendance.Api.Tests.Infrastructure;

/// <summary>
/// The real API over a real SQL Server database. Starting it runs the startup guard, so the tests
/// also prove that the migrations apply and the seed data loads.
/// </summary>
public sealed class AttendanceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Nordwin-Test-Pa55word";

    private static int _lastClientIp;

    private readonly TestSqlServer _sqlServer = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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
        builder.UseSetting("Serilog:MinimumLevel:Default", "Error");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestClientIpStartupFilter>();
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
        });
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

    /// <summary>Adds an active user with <see cref="Password"/>, optionally in one of the seeded system roles.</summary>
    public async Task<TestUser> CreateUserAsync(string? role = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            Email = $"user.{Guid.NewGuid():N}@nordwin.example",
            PasswordHash = passwordHasher.Hash(Password),
        };
        if (role is not null)
        {
            user.UserRoles.Add(new UserRole { Role = await db.Roles.SingleAsync(r => r.Name == role, Ct) });
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return new TestUser(user.Email, user.PublicId);
    }

    /// <summary>Reads the database directly, in its own scope, to check what the API stored.</summary>
    public async Task<T> QueryDbAsync<T>(Func<AttendanceDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AttendanceDbContext>());
    }
}

public sealed record TestUser(string Email, Guid PublicId);

/// <summary>Every API test class joins this collection: one app and one database, tests run one at a time.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<AttendanceApiFactory>
{
    public const string Name = "Api";
}

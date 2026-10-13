using Attendance.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Attendance.Infrastructure.Persistence;

/// <summary>Bound from the <c>Database</c> section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Apply pending EF Core migrations at startup (the startup guard). Turn it off where a separate
    /// migration job owns the schema.
    /// </summary>
    public bool MigrateOnStartup { get; set; } = true;
}

/// <summary>Brings the database up to date at startup: migrations first, then reference data.</summary>
public sealed class DatabaseInitializer(
    AttendanceDbContext db,
    IdentitySeeder identitySeeder,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (options.Value.MigrateOnStartup)
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                logger.LogInformation("Applying {Count} migrations: {Migrations}", pending.Count, pending);
                await db.Database.MigrateAsync(cancellationToken);
            }
        }

        await identitySeeder.SeedAsync(cancellationToken);
    }
}

public static class DatabaseInitializerExtensions
{
    /// <summary>Runs <see cref="DatabaseInitializer"/> in its own DI scope. Call it once, before the app starts serving.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(cancellationToken);
    }
}

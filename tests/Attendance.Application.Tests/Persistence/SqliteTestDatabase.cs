using Attendance.Infrastructure.Persistence;
using Attendance.Infrastructure.Persistence.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Application.Tests.Persistence;

/// <summary>
/// A private in-memory SQLite database with the real model and audit interceptor. The schema is built
/// with EnsureCreated because the SQL Server migrations can't run on SQLite. Real databases use
/// migrations only.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public TestClock Clock { get; } = new(new DateTimeOffset(2026, 10, 1, 3, 30, 0, TimeSpan.Zero));

    public TestCurrentUser CurrentUser { get; } = new();

    public TestDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditableEntityInterceptor(CurrentUser, Clock))
            .Options);

    public void Dispose() => _connection.Dispose();
}

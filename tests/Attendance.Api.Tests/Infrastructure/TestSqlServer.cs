using Attendance.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Attendance.Api.Tests.Infrastructure;

/// <summary>
/// A real SQL Server for the integration tests: a Testcontainers container by default.
/// Where Docker isn't available, set <c>ATTENDANCE_TEST_SQLSERVER</c> to a connection string for an
/// existing server, without a database name. Each run then creates its own database there and drops
/// it at the end.
/// </summary>
public sealed class TestSqlServer : IAsyncDisposable
{
    public const string ServerVariable = "ATTENDANCE_TEST_SQLSERVER";

    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04";

    private MsSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var server = Environment.GetEnvironmentVariable(ServerVariable);
        if (string.IsNullOrWhiteSpace(server))
        {
            try
            {
                _container = new MsSqlBuilder(Image).Build();
                await _container.StartAsync(cancellationToken);
            }
            catch (DockerUnavailableException ex)
            {
                throw new InvalidOperationException(
                    $"The integration tests need SQL Server. Start Docker, or set {ServerVariable} to a "
                    + "connection string for an existing server (e.g. Server=.\\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True).",
                    ex);
            }

            server = _container.GetConnectionString();
        }

        ConnectionString = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = $"NordwinAttendance_Test_{Guid.NewGuid():N}",
        }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        if (ConnectionString.Length == 0)
        {
            return;
        }

        // Drop the database this run created, and nothing else.
        SqlConnection.ClearAllPools();
        await using var context = new AttendanceDbContext(
            new DbContextOptionsBuilder<AttendanceDbContext>().UseSqlServer(ConnectionString).Options);
        await context.Database.EnsureDeletedAsync();
    }
}

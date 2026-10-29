using Attendance.Domain.Scheduling;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Application.Tests.Persistence;

/// <summary>
/// The entity derives CrossesMidnight from the times; the database refuses any write, by any route,
/// that contradicts them.
/// </summary>
public sealed class ShiftConstraintTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_database_refuses_a_CrossesMidnight_that_contradicts_the_times()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        context.Shifts.Add(new Shift { Code = "NIGHT", Name = "Night", StartTime = new TimeOnly(22, 0), EndTime = new TimeOnly(6, 0) });
        await context.SaveChangesAsync(Ct);

        var contradict = () => context.Database.ExecuteSqlRawAsync("UPDATE Shifts SET CrossesMidnight = 0", Ct);

        (await contradict.Should().ThrowAsync<SqliteException>()).Which.Message.Should().Contain("CK_Shifts_CrossesMidnight");
    }

    [Fact]
    public async Task The_database_refuses_a_shift_that_ends_when_it_starts()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        context.Shifts.Add(new Shift { Code = "ZERO", Name = "Zero", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 0) });

        var save = () => context.SaveChangesAsync(Ct);

        (await save.Should().ThrowAsync<DbUpdateException>()).WithInnerException<SqliteException>()
            .Which.Message.Should().Contain("CK_Shifts_StartTime_EndTime");
    }
}

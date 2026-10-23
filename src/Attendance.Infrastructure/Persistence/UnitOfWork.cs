using Attendance.Application.Common.Exceptions;
using Attendance.Application.Common.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence;

internal sealed class UnitOfWork(AttendanceDbContext db) : IUnitOfWork
{
    // SQL Server: 2601 = duplicate key in a unique index, 2627 = unique or primary key constraint.
    private const int DuplicateKeyInIndex = 2601;
    private const int DuplicateKeyInConstraint = 2627;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("A row was changed by another request after it was read.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: DuplicateKeyInIndex or DuplicateKeyInConstraint })
        {
            throw new DuplicateKeyException("A row with the same unique key already exists.", ex);
        }
    }

    public void DiscardChanges() => db.ChangeTracker.Clear();
}

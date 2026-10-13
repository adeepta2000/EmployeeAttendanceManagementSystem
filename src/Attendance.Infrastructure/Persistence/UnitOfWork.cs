using Attendance.Application.Common.Exceptions;
using Attendance.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence;

internal sealed class UnitOfWork(AttendanceDbContext db) : IUnitOfWork
{
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
    }

    public void DiscardChanges() => db.ChangeTracker.Clear();
}

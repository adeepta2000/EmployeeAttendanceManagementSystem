using Attendance.Application.Common.Events;
using Attendance.Application.Common.Exceptions;
using Attendance.Application.Common.Interfaces;
using Attendance.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence;

internal sealed class UnitOfWork(AttendanceDbContext db, IDomainEventDispatcher domainEvents) : IUnitOfWork
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

        await DispatchDomainEventsAsync(cancellationToken);
    }

    // Only after a successful save, so handlers see the saved state; a failed save leaves the events on
    // their entities. Each event is taken off its entity before dispatch, so it is dispatched once.
    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var raisers = db.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();
        if (raisers.Count == 0)
        {
            return;
        }

        var events = raisers.SelectMany(entity => entity.DomainEvents).ToList();
        raisers.ForEach(entity => entity.ClearDomainEvents());
        await domainEvents.DispatchAsync(events, cancellationToken);
    }

    public void DiscardChanges() => db.ChangeTracker.Clear();
}

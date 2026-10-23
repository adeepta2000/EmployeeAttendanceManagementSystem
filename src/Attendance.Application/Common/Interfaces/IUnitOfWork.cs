using Attendance.Application.Common.Exceptions;

namespace Attendance.Application.Common.Interfaces;

/// <summary>Commits the changes that repositories have tracked during the current request.</summary>
public interface IUnitOfWork
{
    /// <summary>Saves every pending change in one transaction.</summary>
    /// <exception cref="ConcurrencyConflictException">A row being updated was changed by another request since it was read.</exception>
    /// <exception cref="DuplicateKeyException">A row would break a unique index, e.g. a code taken by a concurrent request.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Forgets every pending change and loaded entity, so the next read starts from the database.</summary>
    void DiscardChanges();
}

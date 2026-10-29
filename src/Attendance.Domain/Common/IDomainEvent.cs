namespace Attendance.Domain.Common;

/// <summary>
/// Something that happened in the domain that other modules react to. Events are dispatched in process,
/// after the change that raised them has been saved.
/// </summary>
public interface IDomainEvent;

/// <summary>An entity that raises domain events. The unit of work dispatches and clears them after a successful save.</summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}

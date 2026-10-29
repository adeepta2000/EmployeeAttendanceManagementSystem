using System.Collections.Concurrent;
using System.Reflection;
using Attendance.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Attendance.Application.Common.Events;

/// <summary>Reacts to one type of domain event. An event may have any number of handlers.</summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Passes domain events to their handlers, in order. The unit of work calls it after a successful save
/// for events raised by entities; a set-based write with no entities calls it directly.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}

internal sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    private static readonly MethodInfo DispatchOneMethod =
        typeof(DomainEventDispatcher).GetMethod(nameof(DispatchOneAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    // One compiled call per event type, so dispatch needs no reflection after the first event of a type.
    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, CancellationToken, Task>> Dispatchers = new();

    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            var dispatch = Dispatchers.GetOrAdd(
                domainEvent.GetType(),
                type => DispatchOneMethod.MakeGenericMethod(type)
                    .CreateDelegate<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>());
            await dispatch(services, domainEvent, cancellationToken);
        }
    }

    private static async Task DispatchOneAsync<TEvent>(IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
        {
            await handler.HandleAsync((TEvent)domainEvent, cancellationToken);
        }
    }
}

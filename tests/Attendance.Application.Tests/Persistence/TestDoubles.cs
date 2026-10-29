using Attendance.Application.Common.Events;
using Attendance.Application.Common.Interfaces;
using Attendance.Domain.Common;
using Attendance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Application.Tests.Persistence;

/// <summary>Keeps every domain event the unit of work dispatches instead of running handlers.</summary>
internal sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> Dispatched { get; } = [];

    public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        Dispatched.AddRange(domainEvents);
        return Task.CompletedTask;
    }
}

internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class TestCurrentUser : ICurrentUser
{
    public string? UserId { get; set; }

    public IReadOnlyCollection<string> Permissions { get; set; } = [];
}

/// <summary>The production model plus <see cref="SoftDeletableProbe"/>.</summary>
internal sealed class TestDbContext(DbContextOptions<AttendanceDbContext> options) : AttendanceDbContext(options)
{
    public DbSet<SoftDeletableProbe> SoftDeletableProbes => Set<SoftDeletableProbe>();
}

/// <summary>
/// No Identity or Organization table is transactional, so none is soft-deletable yet. This stand-in
/// exercises the soft-delete plumbing until the Phase 3 tables arrive.
/// </summary>
internal sealed class SoftDeletableProbe : BaseEntity, ISoftDeletable
{
    public required string Name { get; set; }

    public bool IsDeleted { get; private set; }
}

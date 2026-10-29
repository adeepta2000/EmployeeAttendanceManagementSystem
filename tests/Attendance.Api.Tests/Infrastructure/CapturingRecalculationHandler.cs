using System.Collections.Concurrent;
using Attendance.Application.Common.Events;
using Attendance.Domain.Processing;

namespace Attendance.Api.Tests.Infrastructure;

/// <summary>
/// Runs beside the real (stub) handler and keeps every recalculation request, so a test can check that a
/// change asked for one (FR-2.5).
/// </summary>
public sealed class CapturingRecalculationHandler : IDomainEventHandler<AttendanceRecalculationRequested>
{
    private readonly ConcurrentQueue<AttendanceRecalculationRequested> _requests = new();

    public Task HandleAsync(AttendanceRecalculationRequested domainEvent, CancellationToken cancellationToken)
    {
        _requests.Enqueue(domainEvent);
        return Task.CompletedTask;
    }

    /// <summary>The requests that cover an employee, oldest first. Tests share the app, so filter by an employee of your own.</summary>
    public IReadOnlyList<AttendanceRecalculationRequested> For(TestEmployee employee) =>
        [.. _requests.Where(r => r.EmployeeIds.Contains(employee.Id))];
}

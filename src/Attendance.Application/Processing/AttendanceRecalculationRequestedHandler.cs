using Attendance.Application.Common.Events;
using Attendance.Domain.Processing;
using Microsoft.Extensions.Logging;

namespace Attendance.Application.Processing;

/// <summary>
/// Processes the affected dates again when an input of attendance processing changes (FR-2.5, spec §2.7).
/// A stub until the processing engine exists.
/// </summary>
internal sealed class AttendanceRecalculationRequestedHandler(ILogger<AttendanceRecalculationRequestedHandler> logger)
    : IDomainEventHandler<AttendanceRecalculationRequested>
{
    public Task HandleAsync(AttendanceRecalculationRequested domainEvent, CancellationToken cancellationToken)
    {
        // TODO(Phase 3): enqueue a Hangfire job that runs ProcessAttendanceCommand for the dates in the range
        // that already have AttendanceDaily rows and are not in a locked period (FR-10.1). Keep it a job rather
        // than inline work: one bulk roster assignment can cover tens of thousands of employee-days.
        logger.LogInformation(
            "Attendance recalculation requested ({Reason}) for {EmployeeCount} employees from {From} to {To}; processing arrives in Phase 3",
            domainEvent.Reason, domainEvent.EmployeeIds.Count, domainEvent.From, domainEvent.To);

        return Task.CompletedTask;
    }
}

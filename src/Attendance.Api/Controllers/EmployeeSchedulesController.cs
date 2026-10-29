using Attendance.Api.Contracts.Scheduling;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Scheduling;
using Attendance.Application.Scheduling.EmployeeSchedules;
using Attendance.Domain.Identity;
using Attendance.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// An employee's default shift and weekly-off pattern (FR-2.2, FR-2.3), and their resolved calendar.
/// Data-scoped like the employee endpoints (FR-8.3): an employee outside your scope gets 404.
/// </summary>
[Route("api/employees/{publicId:guid}")]
public sealed class EmployeeSchedulesController : ApiControllerBase
{
    /// <response code="404">No such employee, or not one you can see.</response>
    [HttpGet("schedule")]
    [Authorize(Policy = Permissions.Employee.Read)]
    [ProducesResponseType<EmployeeScheduleDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetSchedule(
        Guid publicId,
        [FromServices] IQueryHandler<GetEmployeeScheduleQuery, Result<EmployeeScheduleDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new GetEmployeeScheduleQuery(publicId), cancellationToken));

    /// <summary>
    /// Assigns the default shift and weekly-off pattern; null clears either. It applies to every date
    /// processed from now on; to change a date already processed, roster it instead.
    /// </summary>
    /// <response code="400">A shift or pattern that doesn't exist or is inactive.</response>
    /// <response code="404">No such employee, or not one you can see.</response>
    [HttpPut("schedule")]
    [Authorize(Policy = Permissions.Roster.Write)]
    [ProducesResponseType<EmployeeScheduleDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> SetSchedule(
        Guid publicId,
        EmployeeScheduleRequest request,
        [FromServices] ICommandHandler<SetEmployeeScheduleCommand, Result<EmployeeScheduleDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(request.ToCommand(publicId), cancellationToken));

    /// <summary>
    /// Each date's day type (working, weekly off or holiday) and effective shift, over up to 92 days.
    /// </summary>
    /// <response code="404">No such employee, or not one you can see.</response>
    [HttpGet("calendar")]
    [Authorize(Policy = Permissions.Employee.Read)]
    [ProducesResponseType<IReadOnlyList<CalendarDayDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetCalendar(
        Guid publicId,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromServices] IQueryHandler<GetEmployeeCalendarQuery, Result<IReadOnlyList<CalendarDayDto>>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new GetEmployeeCalendarQuery(publicId, fromDate, toDate), cancellationToken));
}

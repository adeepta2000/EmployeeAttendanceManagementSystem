using Attendance.Api.Contracts.Scheduling;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Scheduling;
using Attendance.Application.Scheduling.Rosters;
using Attendance.Domain.Identity;
using Attendance.Shared.Paging;
using Attendance.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// Roster entries: a shift, and optionally a weekly-off override, for one employee on one date (FR-2.3,
/// FR-2.4). Reading needs <c>employee.read</c>, changing needs <c>roster.write</c>, and every endpoint is
/// data-scoped (FR-8.3): an entry of an employee outside your scope gets 404, as if it didn't exist.
/// Changing an entry requests a recalculation of that date (FR-2.5).
/// </summary>
[Route("api/rosters")]
public sealed class RostersController : ApiControllerBase
{
    /// <summary>Lists the roster entries in your scope over a date range, by date and employee code.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Employee.Read)]
    [ProducesResponseType<PagedResult<RosterEntryDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> List(
        [FromQuery] RosterListRequest request,
        [FromServices] IQueryHandler<ListRosterEntriesQuery, PagedResult<RosterEntryDto>> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken));

    /// <summary>
    /// The team roster grid: a page of the active employees in your scope, by code, each with every date's
    /// day type and effective shift (screen 16).
    /// </summary>
    [HttpGet("calendar")]
    [Authorize(Policy = Permissions.Employee.Read)]
    [ProducesResponseType<PagedResult<TeamCalendarRowDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> TeamCalendar(
        [FromQuery] TeamCalendarRequest request,
        [FromServices] IQueryHandler<ListTeamCalendarQuery, PagedResult<TeamCalendarRowDto>> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken));

    /// <response code="404">No such entry, or not one you can see.</response>
    [HttpGet("{publicId:guid}")]
    [Authorize(Policy = Permissions.Employee.Read)]
    [ProducesResponseType<RosterEntryDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(
        Guid publicId,
        [FromServices] IQueryHandler<GetRosterEntryQuery, Result<RosterEntryDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new GetRosterEntryQuery(publicId), cancellationToken));

    /// <summary>Rosters a shift for one employee on one date.</summary>
    /// <response code="400">Invalid fields, an employee outside your scope or inactive, an inactive shift, or a date before joining.</response>
    /// <response code="409">The employee is already rostered on that date: change that entry instead.</response>
    [HttpPost]
    [Authorize(Policy = Permissions.Roster.Write)]
    [ProducesResponseType<RosterEntryDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create(
        CreateRosterEntryRequest request,
        [FromServices] ICommandHandler<CreateRosterEntryCommand, Result<RosterEntryDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { publicId = result.Value.PublicId }, result.Value)
            : Problem(result.Error);
    }

    /// <summary>Changes an entry's shift, weekly-off override or remarks. The employee and date can't change.</summary>
    /// <response code="404">No such entry, or not one you can see.</response>
    [HttpPut("{publicId:guid}")]
    [Authorize(Policy = Permissions.Roster.Write)]
    [ProducesResponseType<RosterEntryDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Update(
        Guid publicId,
        UpdateRosterEntryRequest request,
        [FromServices] ICommandHandler<UpdateRosterEntryCommand, Result<RosterEntryDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(request.ToCommand(publicId), cancellationToken));

    /// <summary>Removes an entry (a soft delete): the date falls back to the default shift and weekly-off pattern.</summary>
    /// <response code="404">No such entry, or not one you can see.</response>
    [HttpDelete("{publicId:guid}")]
    [Authorize(Policy = Permissions.Roster.Write)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(
        Guid publicId,
        [FromServices] ICommandHandler<DeleteRosterEntryCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteRosterEntryCommand(publicId), cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }

    /// <summary>
    /// Rosters one shift for every active employee of a department within your scope, over up to 92 days
    /// (FR-2.4). Existing entries in the range are overwritten; dates before an employee joined are skipped.
    /// </summary>
    /// <response code="400">Invalid fields, an unknown department, or an inactive shift.</response>
    [HttpPost("bulk")]
    [Authorize(Policy = Permissions.Roster.Write)]
    [ProducesResponseType<BulkRosterResult>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> BulkAssign(
        BulkRosterRequest request,
        [FromServices] ICommandHandler<BulkAssignRosterCommand, Result<BulkRosterResult>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(request.ToCommand(), cancellationToken));

    /// <summary>
    /// Rosters one shift for a selection of employees over up to 92 days: a block drag-selected on the team
    /// roster. Existing entries in the range are overwritten; dates before an employee joined are skipped.
    /// </summary>
    /// <response code="400">Invalid fields, an employee outside your scope or inactive, or an inactive shift.</response>
    [HttpPost("assign")]
    [Authorize(Policy = Permissions.Roster.Write)]
    [ProducesResponseType<BulkRosterResult>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Assign(
        AssignRosterRequest request,
        [FromServices] ICommandHandler<AssignRosterCommand, Result<BulkRosterResult>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(request.ToCommand(), cancellationToken));

    /// <summary>
    /// Removes the roster entries of a selection of employees over up to 92 days, so those dates fall back
    /// to each employee's default shift and weekly-off pattern.
    /// </summary>
    /// <response code="400">Invalid fields, or an employee outside your scope or inactive.</response>
    [HttpPost("clear")]
    [Authorize(Policy = Permissions.Roster.Write)]
    [ProducesResponseType<ClearRosterResult>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Clear(
        ClearRosterRequest request,
        [FromServices] ICommandHandler<ClearRosterCommand, Result<ClearRosterResult>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(request.ToCommand(), cancellationToken));
}

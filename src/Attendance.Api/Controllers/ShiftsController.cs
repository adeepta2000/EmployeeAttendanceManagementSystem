using Attendance.Api.Contracts.Scheduling;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Scheduling;
using Attendance.Application.Scheduling.Shifts;
using Attendance.Domain.Identity;
using Attendance.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// Shift definitions (FR-2.1, screen 29). Any signed-in user can read them; changing them needs
/// <c>shift.write</c>. They are retired, never deleted. <c>crossesMidnight</c> is derived from the times.
/// </summary>
[Route("api/shifts")]
public sealed class ShiftsController : ApiControllerBase
{
    /// <summary>Lists shifts by name.</summary>
    /// <param name="isActive">True for active shifts only, false for retired ones only; omit for both.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ShiftDto>>(StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> List(
        [FromQuery] bool? isActive,
        [FromServices] IQueryHandler<ListShiftsQuery, IReadOnlyList<ShiftDto>> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new ListShiftsQuery(isActive), cancellationToken));

    [HttpGet("{publicId:guid}")]
    [ProducesResponseType<ShiftDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(
        Guid publicId,
        [FromServices] IQueryHandler<GetShiftQuery, Result<ShiftDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new GetShiftQuery(publicId), cancellationToken));

    /// <response code="400">Invalid fields, including a <c>crossesMidnight</c> that disagrees with the times.</response>
    /// <response code="409">The code is already in use.</response>
    [HttpPost]
    [Authorize(Policy = Permissions.Shift.Write)]
    [ProducesResponseType<ShiftDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create(
        ShiftRequest request,
        [FromServices] ICommandHandler<CreateShiftCommand, Result<ShiftDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCreateCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { publicId = result.Value.PublicId }, result.Value)
            : Problem(result.Error);
    }

    /// <summary>Replaces a shift's definition. It applies to every date processed from now on.</summary>
    /// <response code="409">The new code is already in use.</response>
    [HttpPut("{publicId:guid}")]
    [Authorize(Policy = Permissions.Shift.Write)]
    [ProducesResponseType<ShiftDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Update(
        Guid publicId,
        ShiftRequest request,
        [FromServices] ICommandHandler<UpdateShiftCommand, Result<ShiftDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(request.ToUpdateCommand(publicId), cancellationToken));

    /// <response code="409">It is still an active employee's default shift.</response>
    [HttpPost("{publicId:guid}/deactivate")]
    [Authorize(Policy = Permissions.Shift.Write)]
    [ProducesResponseType<ShiftDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Deactivate(
        Guid publicId,
        [FromServices] ICommandHandler<SetShiftActiveCommand, Result<ShiftDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new SetShiftActiveCommand(publicId, IsActive: false), cancellationToken));

    [HttpPost("{publicId:guid}/activate")]
    [Authorize(Policy = Permissions.Shift.Write)]
    [ProducesResponseType<ShiftDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Activate(
        Guid publicId,
        [FromServices] ICommandHandler<SetShiftActiveCommand, Result<ShiftDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new SetShiftActiveCommand(publicId, IsActive: true), cancellationToken));
}

using Attendance.Api.Contracts.Organization;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization;
using Attendance.Application.Organization.WorkLocations;
using Attendance.Domain.Identity;
using Attendance.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// Work locations and their optional geofences (FR-1.1, FR-3.2). Any signed-in user can read them;
/// changing them needs <c>org.write</c>. They are retired, never deleted.
/// </summary>
[Route("api/work-locations")]
public sealed class WorkLocationsController : ApiControllerBase
{
    /// <summary>Lists work locations by name.</summary>
    /// <param name="isActive">True for active locations only, false for retired ones only; omit for both.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WorkLocationDto>>(StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> List(
        [FromQuery] bool? isActive,
        [FromServices] IQueryHandler<ListWorkLocationsQuery, IReadOnlyList<WorkLocationDto>> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new ListWorkLocationsQuery(isActive), cancellationToken));

    [HttpGet("{publicId:guid}")]
    [ProducesResponseType<WorkLocationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(
        Guid publicId,
        [FromServices] IQueryHandler<GetWorkLocationQuery, Result<WorkLocationDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new GetWorkLocationQuery(publicId), cancellationToken));

    /// <response code="409">The code is already in use.</response>
    [HttpPost]
    [Authorize(Policy = Permissions.Org.Write)]
    [ProducesResponseType<WorkLocationDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create(
        WorkLocationRequest request,
        [FromServices] ICommandHandler<CreateWorkLocationCommand, Result<WorkLocationDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCreateCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { publicId = result.Value.PublicId }, result.Value)
            : Problem(result.Error);
    }

    /// <response code="409">The new code is already in use.</response>
    [HttpPut("{publicId:guid}")]
    [Authorize(Policy = Permissions.Org.Write)]
    [ProducesResponseType<WorkLocationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Update(
        Guid publicId,
        WorkLocationRequest request,
        [FromServices] ICommandHandler<UpdateWorkLocationCommand, Result<WorkLocationDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(request.ToUpdateCommand(publicId), cancellationToken));

    /// <response code="409">Active employees are still assigned to this location.</response>
    [HttpPost("{publicId:guid}/deactivate")]
    [Authorize(Policy = Permissions.Org.Write)]
    [ProducesResponseType<WorkLocationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Deactivate(
        Guid publicId,
        [FromServices] ICommandHandler<SetWorkLocationActiveCommand, Result<WorkLocationDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new SetWorkLocationActiveCommand(publicId, IsActive: false), cancellationToken));

    [HttpPost("{publicId:guid}/activate")]
    [Authorize(Policy = Permissions.Org.Write)]
    [ProducesResponseType<WorkLocationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Activate(
        Guid publicId,
        [FromServices] ICommandHandler<SetWorkLocationActiveCommand, Result<WorkLocationDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new SetWorkLocationActiveCommand(publicId, IsActive: true), cancellationToken));
}

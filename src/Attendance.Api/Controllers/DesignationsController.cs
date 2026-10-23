using Attendance.Api.Contracts.Organization;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization;
using Attendance.Application.Organization.Designations;
using Attendance.Domain.Identity;
using Attendance.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// Designations (FR-1.1, screen 28). Any signed-in user can read them; changing them needs
/// <c>org.write</c>. They are retired, never deleted.
/// </summary>
[Route("api/designations")]
public sealed class DesignationsController : ApiControllerBase
{
    /// <summary>Lists designations by name.</summary>
    /// <param name="isActive">True for active designations only, false for retired ones only; omit for both.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DesignationDto>>(StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> List(
        [FromQuery] bool? isActive,
        [FromServices] IQueryHandler<ListDesignationsQuery, IReadOnlyList<DesignationDto>> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new ListDesignationsQuery(isActive), cancellationToken));

    [HttpGet("{publicId:guid}")]
    [ProducesResponseType<DesignationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(
        Guid publicId,
        [FromServices] IQueryHandler<GetDesignationQuery, Result<DesignationDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new GetDesignationQuery(publicId), cancellationToken));

    /// <response code="409">The code is already in use.</response>
    [HttpPost]
    [Authorize(Policy = Permissions.Org.Write)]
    [ProducesResponseType<DesignationDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create(
        DesignationRequest request,
        [FromServices] ICommandHandler<CreateDesignationCommand, Result<DesignationDto>> handler,
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
    [ProducesResponseType<DesignationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Update(
        Guid publicId,
        DesignationRequest request,
        [FromServices] ICommandHandler<UpdateDesignationCommand, Result<DesignationDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(request.ToUpdateCommand(publicId), cancellationToken));

    /// <response code="409">Active employees still hold this designation.</response>
    [HttpPost("{publicId:guid}/deactivate")]
    [Authorize(Policy = Permissions.Org.Write)]
    [ProducesResponseType<DesignationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Deactivate(
        Guid publicId,
        [FromServices] ICommandHandler<SetDesignationActiveCommand, Result<DesignationDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new SetDesignationActiveCommand(publicId, IsActive: false), cancellationToken));

    [HttpPost("{publicId:guid}/activate")]
    [Authorize(Policy = Permissions.Org.Write)]
    [ProducesResponseType<DesignationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Activate(
        Guid publicId,
        [FromServices] ICommandHandler<SetDesignationActiveCommand, Result<DesignationDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new SetDesignationActiveCommand(publicId, IsActive: true), cancellationToken));
}

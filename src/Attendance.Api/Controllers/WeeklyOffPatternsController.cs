using Attendance.Api.Contracts.Scheduling;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Scheduling;
using Attendance.Application.Scheduling.WeeklyOffPatterns;
using Attendance.Domain.Identity;
using Attendance.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// Weekly-off patterns (FR-2.2, screen 30), e.g. every Friday, or Friday plus the 2nd and 4th Saturday.
/// Any signed-in user can read them; changing them needs <c>shift.write</c>. A pattern's days are fixed
/// once it exists: only its name can change. They are retired, never deleted.
/// </summary>
[Route("api/weekly-off-patterns")]
public sealed class WeeklyOffPatternsController : ApiControllerBase
{
    /// <summary>Lists patterns by name.</summary>
    /// <param name="isActive">True for active patterns only, false for retired ones only; omit for both.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WeeklyOffPatternDto>>(StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> List(
        [FromQuery] bool? isActive,
        [FromServices] IQueryHandler<ListWeeklyOffPatternsQuery, IReadOnlyList<WeeklyOffPatternDto>> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new ListWeeklyOffPatternsQuery(isActive), cancellationToken));

    [HttpGet("{publicId:guid}")]
    [ProducesResponseType<WeeklyOffPatternDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(
        Guid publicId,
        [FromServices] IQueryHandler<GetWeeklyOffPatternQuery, Result<WeeklyOffPatternDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new GetWeeklyOffPatternQuery(publicId), cancellationToken));

    /// <response code="409">The name is already in use.</response>
    [HttpPost]
    [Authorize(Policy = Permissions.Shift.Write)]
    [ProducesResponseType<WeeklyOffPatternDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create(
        CreateWeeklyOffPatternRequest request,
        [FromServices] ICommandHandler<CreateWeeklyOffPatternCommand, Result<WeeklyOffPatternDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { publicId = result.Value.PublicId }, result.Value)
            : Problem(result.Error);
    }

    /// <summary>Renames a pattern. Its days can't change: create a new pattern instead.</summary>
    /// <response code="409">The new name is already in use.</response>
    [HttpPut("{publicId:guid}")]
    [Authorize(Policy = Permissions.Shift.Write)]
    [ProducesResponseType<WeeklyOffPatternDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Rename(
        Guid publicId,
        RenameWeeklyOffPatternRequest request,
        [FromServices] ICommandHandler<RenameWeeklyOffPatternCommand, Result<WeeklyOffPatternDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new RenameWeeklyOffPatternCommand(publicId, request.Name), cancellationToken));

    /// <response code="409">Active employees still follow this pattern.</response>
    [HttpPost("{publicId:guid}/deactivate")]
    [Authorize(Policy = Permissions.Shift.Write)]
    [ProducesResponseType<WeeklyOffPatternDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Deactivate(
        Guid publicId,
        [FromServices] ICommandHandler<SetWeeklyOffPatternActiveCommand, Result<WeeklyOffPatternDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new SetWeeklyOffPatternActiveCommand(publicId, IsActive: false), cancellationToken));

    [HttpPost("{publicId:guid}/activate")]
    [Authorize(Policy = Permissions.Shift.Write)]
    [ProducesResponseType<WeeklyOffPatternDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Activate(
        Guid publicId,
        [FromServices] ICommandHandler<SetWeeklyOffPatternActiveCommand, Result<WeeklyOffPatternDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new SetWeeklyOffPatternActiveCommand(publicId, IsActive: true), cancellationToken));
}

using Attendance.Api.Contracts.Scheduling;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Scheduling;
using Attendance.Application.Scheduling.Holidays;
using Attendance.Domain.Identity;
using Attendance.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// The public-holiday calendar (FR-7.1, screen 32). A holiday applies everywhere unless limited to some
/// work locations. Any signed-in user can read it; changing it needs <c>holiday.write</c>.
/// </summary>
[Route("api/holidays")]
public sealed class HolidaysController : ApiControllerBase
{
    /// <summary>Lists a year's holidays by date.</summary>
    /// <param name="year">The calendar year.</param>
    /// <param name="workLocationId">PublicId of a work location: only the holidays that apply there, including those that apply everywhere.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<HolidayDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> List(
        [FromQuery] int year,
        [FromQuery] Guid? workLocationId,
        [FromServices] IQueryHandler<ListHolidaysQuery, IReadOnlyList<HolidayDto>> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(new ListHolidaysQuery(year, workLocationId), cancellationToken));

    [HttpGet("{publicId:guid}")]
    [ProducesResponseType<HolidayDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(
        Guid publicId,
        [FromServices] IQueryHandler<GetHolidayQuery, Result<HolidayDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new GetHolidayQuery(publicId), cancellationToken));

    /// <response code="400">Invalid fields, or a work location that doesn't exist or is inactive.</response>
    /// <response code="409">A holiday with the same name is already on that date.</response>
    [HttpPost]
    [Authorize(Policy = Permissions.Holiday.Write)]
    [ProducesResponseType<HolidayDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create(
        HolidayRequest request,
        [FromServices] ICommandHandler<CreateHolidayCommand, Result<HolidayDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCreateCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { publicId = result.Value.PublicId }, result.Value)
            : Problem(result.Error);
    }

    /// <response code="409">A holiday with the same name is already on that date.</response>
    [HttpPut("{publicId:guid}")]
    [Authorize(Policy = Permissions.Holiday.Write)]
    [ProducesResponseType<HolidayDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Update(
        Guid publicId,
        HolidayRequest request,
        [FromServices] ICommandHandler<UpdateHolidayCommand, Result<HolidayDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(request.ToUpdateCommand(publicId), cancellationToken));

    /// <summary>Takes a holiday off the calendar (a soft delete).</summary>
    [HttpDelete("{publicId:guid}")]
    [Authorize(Policy = Permissions.Holiday.Write)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(
        Guid publicId,
        [FromServices] ICommandHandler<DeleteHolidayCommand, Result> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteHolidayCommand(publicId), cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(result.Error);
    }
}

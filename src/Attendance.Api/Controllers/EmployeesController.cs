using Attendance.Api.Contracts.Organization;
using Attendance.Application.Common.Files;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Organization;
using Attendance.Application.Organization.Employees;
using Attendance.Application.Organization.Employees.Import;
using Attendance.Domain.Identity;
using Attendance.Shared.Paging;
using Attendance.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

/// <summary>
/// The employee master (FR-1.2 to FR-1.5, screens 26 and 27). Every endpoint is data-scoped by its
/// handler (FR-8.3): an employee sees themselves, a manager their reporting tree, HR the whole company.
/// An employee outside the caller's scope gets 404, exactly as if it didn't exist; 403 only ever means
/// a missing permission.
/// </summary>
[Route("api/employees")]
public sealed class EmployeesController : ApiControllerBase
{
    // The upload cap plus room for the multipart framing around it.
    private const long ImportRequestLimit = EmployeeImportLimits.MaxFileBytes + 64 * 1024;

    /// <summary>Lists the employees in your scope, filtered, sorted and paged.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Employee.Read)]
    [ProducesResponseType<PagedResult<EmployeeSummaryDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> List(
        [FromQuery] EmployeeListRequest request,
        [FromServices] IQueryHandler<ListEmployeesQuery, PagedResult<EmployeeSummaryDto>> handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(request.ToQuery(), cancellationToken));

    /// <response code="404">No such employee, or not one you can see.</response>
    [HttpGet("{publicId:guid}")]
    [Authorize(Policy = Permissions.Employee.Read)]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(
        Guid publicId,
        [FromServices] IQueryHandler<GetEmployeeQuery, Result<EmployeeDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new GetEmployeeQuery(publicId), cancellationToken));

    /// <summary>Adds an employee. No login account is created (FR-1.3).</summary>
    /// <response code="409">The employee code is already in use.</response>
    [HttpPost]
    [Authorize(Policy = Permissions.Employee.Write)]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create(
        EmployeeRequest request,
        [FromServices] ICommandHandler<CreateEmployeeCommand, Result<EmployeeDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToCreateCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { publicId = result.Value.PublicId }, result.Value)
            : Problem(result.Error);
    }

    /// <response code="404">No such employee, or not one you can see.</response>
    /// <response code="409">The new employee code is already in use.</response>
    [HttpPut("{publicId:guid}")]
    [Authorize(Policy = Permissions.Employee.Write)]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Update(
        Guid publicId,
        EmployeeRequest request,
        [FromServices] ICommandHandler<UpdateEmployeeCommand, Result<EmployeeDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(request.ToUpdateCommand(publicId), cancellationToken));

    /// <summary>
    /// Records the employee's separation (FR-1.5). Nothing is deleted; their login account, if any, is
    /// deactivated so they can no longer sign in.
    /// </summary>
    /// <response code="404">No such employee, or not one you can see.</response>
    /// <response code="409">The employee is already inactive.</response>
    [HttpPost("{publicId:guid}/deactivate")]
    [Authorize(Policy = Permissions.Employee.Write)]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Deactivate(
        Guid publicId,
        DeactivateEmployeeRequest request,
        [FromServices] ICommandHandler<DeactivateEmployeeCommand, Result<EmployeeDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new DeactivateEmployeeCommand(publicId, request.SeparationDate), cancellationToken));

    /// <summary>Reverses a deactivation. A login account deactivated with it stays inactive.</summary>
    /// <response code="404">No such employee, or not one you can see.</response>
    /// <response code="409">The employee is already active.</response>
    [HttpPost("{publicId:guid}/reactivate")]
    [Authorize(Policy = Permissions.Employee.Write)]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Reactivate(
        Guid publicId,
        [FromServices] ICommandHandler<ReactivateEmployeeCommand, Result<EmployeeDto>> handler,
        CancellationToken cancellationToken) =>
        ToActionResult(await handler.HandleAsync(new ReactivateEmployeeCommand(publicId), cancellationToken));

    /// <summary>
    /// Bulk-imports employees from an .xlsx file (FR-1.4). Valid rows are imported; invalid rows are
    /// listed with their row number, column and reason. Use <c>dryRun=true</c> to validate without saving.
    /// </summary>
    /// <response code="200">The file was processed. Check <c>errors</c>: some or all rows may have failed.</response>
    /// <response code="400">The file itself was rejected: wrong type, too large, unreadable or missing columns.</response>
    [HttpPost("import")]
    [Authorize(Policy = Permissions.Employee.Import)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImportRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportRequestLimit)]
    [ProducesResponseType<EmployeeImportResult>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Import(
        IFormFile file,
        [FromQuery] bool dryRun,
        [FromServices] ICommandHandler<ImportEmployeesCommand, Result<EmployeeImportResult>> handler,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var upload = new UploadedFile(file.FileName, file.ContentType, file.Length, content);
        return ToActionResult(await handler.HandleAsync(new ImportEmployeesCommand(upload, dryRun), cancellationToken));
    }
}

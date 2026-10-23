using Attendance.Application.Common.Files;
using Attendance.Application.Common.Interfaces;
using Attendance.Application.Common.Messaging;
using Attendance.Application.Common.Security;
using Attendance.Application.Organization.Abstractions;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Columns = Attendance.Application.Organization.Employees.Import.EmployeeImportColumns;

namespace Attendance.Application.Organization.Employees.Import;

/// <remarks>
/// Checks run in stages, and every stage looks at every row, so one upload reports everything wrong with
/// the file rather than one problem at a time:
/// <list type="number">
/// <item>Each row's own values: required, formats, lengths, dates (the same rules as the create command).</item>
/// <item>Employee codes repeated within the file.</item>
/// <item>Department, designation and work location codes: must name an active row.</item>
/// <item>Employee codes already in the database, and reporting managers. A manager is either an existing
/// active employee inside the caller's scope (FR-8.3) or another row of the same file.</item>
/// <item>Rows whose managers report to each other in a loop.</item>
/// <item>Rows whose in-file manager failed: they fail too, as they would point at an employee that doesn't exist.</item>
/// </list>
/// The rows left are saved in one transaction; EF inserts in-file managers before their reports.
/// </remarks>
internal sealed class ImportEmployeesCommandHandler(
    ISpreadsheetReader reader,
    IEmployeeRepository employees,
    IDepartmentRepository departments,
    IMasterDataRepository<Designation> designations,
    IMasterDataRepository<WorkLocation> workLocations,
    EmployeeScopeResolver scopes,
    IValidator<EmployeeImportRow> rowValidator,
    IUnitOfWork unitOfWork,
    ILogger<ImportEmployeesCommandHandler> logger)
    : ICommandHandler<ImportEmployeesCommand, Result<EmployeeImportResult>>
{
    public async Task<Result<EmployeeImportResult>> HandleAsync(ImportEmployeesCommand command, CancellationToken cancellationToken)
    {
        var sheet = await reader.ReadFirstSheetAsync(command.File.Content, EmployeeImportLimits.MaxRows, cancellationToken);
        if (!sheet.IsSuccess)
        {
            return sheet.Error;
        }

        var layout = EmployeeImportLayout.Create(sheet.Value.Headers);
        if (!layout.IsSuccess)
        {
            return layout.Error;
        }

        if (sheet.Value.Rows.Count == 0)
        {
            return EmployeeImportErrors.NoRows;
        }

        var rows = sheet.Value.Rows.Select(r => ImportRow.Parse(r, layout.Value, rowValidator)).ToList();

        FlagDuplicateCodes(rows);
        await ResolveOrgUnitsAsync(rows, cancellationToken);
        await ResolveEmployeeCodesAsync(rows, cancellationToken);
        FlagReportingLoops(rows);
        FailRowsWithFailedManagers(rows);

        var valid = rows.Where(r => r.IsValid).ToList();
        if (!command.DryRun && valid.Count > 0)
        {
            foreach (var row in valid)
            {
                row.Entity = EmployeeFieldRules.CreateEmployee(row.Data);
                row.Entity.DepartmentId = row.DepartmentId;
                row.Entity.DesignationId = row.DesignationId;
                row.Entity.WorkLocationId = row.WorkLocationId;
                row.Entity.ReportingManagerId = row.ExistingManagerId;
            }

            foreach (var row in valid)
            {
                if (row.ManagerRow is not null)
                {
                    row.Entity!.ReportingManager = row.ManagerRow.Entity;
                }

                employees.Add(row.Entity!);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // OrderBy is stable, so each row's errors stay in the order the checks found them.
        var errors = rows.SelectMany(r => r.Errors).OrderBy(e => e.RowNumber).ToList();
        var result = new EmployeeImportResult(command.DryRun, rows.Count, valid.Count, rows.Count - valid.Count, errors);

        logger.LogInformation(
            "Employee import (dry run: {DryRun}): {ImportedRows} of {TotalRows} rows valid, {FailedRows} failed",
            result.DryRun, result.ImportedRows, result.TotalRows, result.FailedRows);
        return result;
    }

    private static void FlagDuplicateCodes(List<ImportRow> rows)
    {
        var duplicates = rows.Where(r => r.Code is not null)
            .GroupBy(r => r.Code!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var group in duplicates)
        {
            var rowNumbers = string.Join(", ", group.Select(r => r.RowNumber));
            foreach (var row in group)
            {
                row.Fail(Columns.EmployeeCode, $"Employee code '{group.Key}' appears more than once in the file (rows {rowNumbers}).");
            }
        }
    }

    private async Task ResolveOrgUnitsAsync(List<ImportRow> rows, CancellationToken cancellationToken)
    {
        var departmentIds = await departments.GetActiveIdsByCodeAsync(cancellationToken);
        var designationIds = await designations.GetActiveIdsByCodeAsync(cancellationToken);
        var workLocationIds = await workLocations.GetActiveIdsByCodeAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.DepartmentId = row.Lookup(Columns.DepartmentCode, row.Data.DepartmentCode, departmentIds, "Department");
            row.DesignationId = row.Lookup(Columns.DesignationCode, row.Data.DesignationCode, designationIds, "Designation");
            row.WorkLocationId = row.Lookup(Columns.WorkLocationCode, row.Data.WorkLocationCode, workLocationIds, "Work location");
        }
    }

    private async Task ResolveEmployeeCodesAsync(List<ImportRow> rows, CancellationToken cancellationToken)
    {
        // One query covers both checks: codes that already exist, and managers who already exist.
        var codes = rows.Select(r => r.Code).Concat(rows.Select(r => r.ManagerCode))
            .Where(code => OrgCodes.IsWellFormed(code, Employee.EmployeeCodeMaxLength))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
        var existing = (await employees.FindByCodesAsync(codes, cancellationToken))
            .ToDictionary(e => e.EmployeeCode, StringComparer.OrdinalIgnoreCase);
        var scope = await scopes.GetEmployeeScopeAsync(cancellationToken);

        var inFile = rows.Where(r => r.Code is not null)
            .GroupBy(r => r.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (row.Code is not null && existing.ContainsKey(row.Code))
            {
                row.Fail(Columns.EmployeeCode, $"Employee code '{row.Code}' already exists.");
            }

            if (row.ManagerCode is not { } managerCode || row.HasErrorIn(Columns.ReportingManagerCode))
            {
                continue;
            }

            if (string.Equals(managerCode, row.Code, StringComparison.OrdinalIgnoreCase))
            {
                row.Fail(Columns.ReportingManagerCode, "An employee can't report to themselves.");
            }
            else if (existing.TryGetValue(managerCode, out var manager))
            {
                // Outside the caller's scope reads the same as missing, as everywhere else (FR-8.3).
                if (manager.IsActive && scope.Includes(manager.Id))
                {
                    row.ExistingManagerId = manager.Id;
                }
                else
                {
                    row.Fail(Columns.ReportingManagerCode, $"Reporting manager '{managerCode}' was not found or is inactive.");
                }
            }
            else if (inFile.TryGetValue(managerCode, out var managerRow))
            {
                row.ManagerRow = managerRow;
            }
            else
            {
                row.Fail(Columns.ReportingManagerCode, $"Reporting manager '{managerCode}' was not found in the file or among active employees.");
            }
        }
    }

    /// <summary>Follows each row's in-file manager chain once; rows on a loop are flagged (linear time).</summary>
    private static void FlagReportingLoops(List<ImportRow> rows)
    {
        var finished = new HashSet<ImportRow>();
        foreach (var start in rows)
        {
            var path = new List<ImportRow>();
            var onPath = new HashSet<ImportRow>();
            var current = start;
            while (current is not null && !finished.Contains(current) && onPath.Add(current))
            {
                path.Add(current);
                current = current.ManagerRow;
            }

            if (current is not null && onPath.Contains(current))
            {
                var loop = path.Skip(path.IndexOf(current)).ToList();
                var rowNumbers = string.Join(", ", loop.Select(r => r.RowNumber).Order());
                foreach (var row in loop)
                {
                    row.Fail(Columns.ReportingManagerCode, $"Rows {rowNumbers} report to each other in a loop.");
                }
            }

            finished.UnionWith(path);
        }
    }

    private static void FailRowsWithFailedManagers(List<ImportRow> rows)
    {
        // Repeat until nothing changes, so a failure travels down a chain of any length.
        bool changed;
        do
        {
            changed = false;
            foreach (var row in rows.Where(r => r.IsValid && r.ManagerRow is { IsValid: false }))
            {
                row.Fail(
                    Columns.ReportingManagerCode,
                    $"Reporting manager '{row.ManagerCode}' is on row {row.ManagerRow!.RowNumber}, which has errors.");
                changed = true;
            }
        }
        while (changed);
    }

    private sealed class ImportRow
    {
        public required int RowNumber { get; init; }

        public required EmployeeImportRow Data { get; init; }

        /// <summary>The normalized employee code; null when the cell is blank.</summary>
        public string? Code { get; init; }

        /// <summary>The normalized reporting manager code; null when the cell is blank.</summary>
        public string? ManagerCode { get; init; }

        public List<EmployeeImportRowError> Errors { get; } = [];

        public bool IsValid => Errors.Count == 0;

        public long DepartmentId { get; set; }

        public long DesignationId { get; set; }

        public long WorkLocationId { get; set; }

        public long? ExistingManagerId { get; set; }

        public ImportRow? ManagerRow { get; set; }

        public Employee? Entity { get; set; }

        public static ImportRow Parse(SpreadsheetRow sheetRow, EmployeeImportLayout layout, IValidator<EmployeeImportRow> validator)
        {
            var (data, errors) = EmployeeImportRowParser.Parse(sheetRow, layout, validator);
            var row = new ImportRow
            {
                RowNumber = sheetRow.RowNumber,
                Data = data,
                Code = string.IsNullOrWhiteSpace(data.EmployeeCode) ? null : OrgCodes.Normalize(data.EmployeeCode),
                ManagerCode = string.IsNullOrWhiteSpace(data.ReportingManagerCode) ? null : OrgCodes.Normalize(data.ReportingManagerCode),
            };

            foreach (var (column, message) in errors)
            {
                row.Fail(column, message);
            }

            return row;
        }

        public void Fail(string? column, string message) =>
            Errors.Add(new EmployeeImportRowError(RowNumber, OrgCodes.NullIfBlank(Data.EmployeeCode), column, message));

        public bool HasErrorIn(string column) => Errors.Exists(e => e.Column == column);

        /// <summary>The internal ID for a code column, or 0 after recording why there is none.</summary>
        public long Lookup(string column, string code, IReadOnlyDictionary<string, long> activeIds, string label)
        {
            if (HasErrorIn(column))
            {
                return 0;
            }

            if (activeIds.TryGetValue(OrgCodes.Normalize(code), out var id))
            {
                return id;
            }

            Fail(column, $"{label} '{code.Trim()}' was not found or is inactive.");
            return 0;
        }
    }
}

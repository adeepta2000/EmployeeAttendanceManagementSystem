using Attendance.Shared.Results;

namespace Attendance.Application.Organization;

public static class DepartmentErrors
{
    public static readonly Error NotFound = new("department.not_found", "The department was not found.", ErrorType.NotFound);

    public static readonly Error ParentNotFound = new(
        "department.parent_not_found", "The parent department was not found or is inactive.", ErrorType.Validation);

    public static readonly Error ParentCycle = new(
        "department.parent_cycle", "A department can't sit under itself or one of its own sub-departments.", ErrorType.Validation);

    public static readonly Error HeadNotFound = new(
        "department.head_not_found", "The head employee was not found or is inactive.", ErrorType.Validation);

    public static readonly Error HasActiveEmployees = new(
        "department.in_use", "The department still has active employees. Move them before deactivating it.", ErrorType.Conflict);

    public static readonly Error HasActiveChildren = new(
        "department.has_children", "The department has active sub-departments. Deactivate or move them first.", ErrorType.Conflict);

    public static Error CodeTaken(string code) =>
        new("department.code_taken", $"Department code '{code}' is already in use.", ErrorType.Conflict);
}

public static class DesignationErrors
{
    public static readonly Error NotFound = new("designation.not_found", "The designation was not found.", ErrorType.NotFound);

    public static readonly Error InUse = new(
        "designation.in_use", "Active employees still hold this designation. Change theirs before deactivating it.", ErrorType.Conflict);

    public static Error CodeTaken(string code) =>
        new("designation.code_taken", $"Designation code '{code}' is already in use.", ErrorType.Conflict);
}

public static class WorkLocationErrors
{
    public static readonly Error NotFound = new("work_location.not_found", "The work location was not found.", ErrorType.NotFound);

    public static readonly Error InUse = new(
        "work_location.in_use", "Active employees are still assigned here. Move them before deactivating it.", ErrorType.Conflict);

    public static Error CodeTaken(string code) =>
        new("work_location.code_taken", $"Work location code '{code}' is already in use.", ErrorType.Conflict);
}

public static class EmployeeErrors
{
    /// <summary>Also returned for an employee outside the caller's scope (FR-8.3), so the two can't be told apart.</summary>
    public static readonly Error NotFound = new("employee.not_found", "The employee was not found.", ErrorType.NotFound);

    public static readonly Error DepartmentNotFound = new(
        "employee.department_not_found", "The department was not found or is inactive.", ErrorType.Validation);

    public static readonly Error DesignationNotFound = new(
        "employee.designation_not_found", "The designation was not found or is inactive.", ErrorType.Validation);

    public static readonly Error WorkLocationNotFound = new(
        "employee.work_location_not_found", "The work location was not found or is inactive.", ErrorType.Validation);

    public static readonly Error ManagerNotFound = new(
        "employee.manager_not_found", "The reporting manager was not found or is inactive.", ErrorType.Validation);

    public static readonly Error ManagerCycle = new(
        "employee.manager_cycle", "An employee can't report to themselves or to someone in their own reporting tree.", ErrorType.Validation);

    public static readonly Error SeparationBeforeJoining = new(
        "employee.separation_before_joining", "The separation date can't be before the joining date.", ErrorType.Validation);

    public static readonly Error AlreadyInactive = new("employee.already_inactive", "The employee is already inactive.", ErrorType.Conflict);

    public static readonly Error AlreadyActive = new("employee.already_active", "The employee is already active.", ErrorType.Conflict);

    public static Error CodeTaken(string code) =>
        new("employee.code_taken", $"Employee code '{code}' is already in use.", ErrorType.Conflict);
}

/// <summary>
/// Problems with an import file as a whole (see also <c>SpreadsheetErrors</c>). Problems with single rows
/// are reported per row instead.
/// </summary>
public static class EmployeeImportErrors
{
    public static readonly Error NoRows = new("import.no_rows", "The first worksheet has no data rows.", ErrorType.Validation);

    public static Error MissingColumns(IEnumerable<string> columns) =>
        new("import.missing_columns", $"The header row is missing these columns: {string.Join(", ", columns)}.", ErrorType.Validation);
}

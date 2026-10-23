namespace Attendance.Application.Common.Security;

/// <summary>
/// The employees a caller may see (FR-8.3): the whole company, or an explicit set of internal IDs.
/// Repositories apply it as a filter inside the query, so an employee outside the scope is
/// indistinguishable from one that doesn't exist.
/// </summary>
public sealed class EmployeeScope
{
    private readonly HashSet<long>? _employeeIds;

    private EmployeeScope(HashSet<long>? employeeIds) => _employeeIds = employeeIds;

    /// <summary>Every employee (HR, and anyone else holding <c>scope.company</c>).</summary>
    public static EmployeeScope Company { get; } = new(null);

    public bool IsCompanyWide => _employeeIds is null;

    /// <summary>The internal IDs in scope. Empty for a company-wide scope; check <see cref="IsCompanyWide"/> first.</summary>
    public IReadOnlyCollection<long> EmployeeIds => _employeeIds ?? [];

    /// <summary>Only these employees. An empty set means the caller sees no employee at all.</summary>
    public static EmployeeScope Restricted(IEnumerable<long> employeeIds) => new([.. employeeIds]);

    public bool Includes(long employeeId) => _employeeIds is null || _employeeIds.Contains(employeeId);

    public override string ToString() =>
        IsCompanyWide ? "EmployeeScope(Company)" : $"EmployeeScope({_employeeIds!.Count} employees)";
}

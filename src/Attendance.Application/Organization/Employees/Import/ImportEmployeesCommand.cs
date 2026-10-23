using Attendance.Application.Common.Files;
using Attendance.Application.Common.Messaging;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Organization.Employees.Import;

/// <summary>
/// Bulk-imports employees from the first worksheet of an .xlsx file (FR-1.4). Each row is checked on its
/// own: valid rows are imported together and invalid ones come back as row-level errors, so one bad row
/// never sinks the file. Only a problem with the file itself (unreadable, missing columns, too large)
/// fails the whole request.
/// </summary>
/// <param name="DryRun">Validate every row and report, but save nothing: the "preview" step before committing.</param>
public sealed record ImportEmployeesCommand(UploadedFile File, bool DryRun) : ICommand<Result<EmployeeImportResult>>;

/// <param name="ImportedRows">Rows imported, or in a dry run, rows that would be.</param>
/// <param name="Errors">Every problem found, one entry each, ordered by row. A row can have several.</param>
public sealed record EmployeeImportResult(
    bool DryRun,
    int TotalRows,
    int ImportedRows,
    int FailedRows,
    IReadOnlyList<EmployeeImportRowError> Errors);

/// <param name="RowNumber">The worksheet row, as numbered in Excel (the header is row 1).</param>
/// <param name="EmployeeCode">The row's employee code as written, to help find it; null when blank.</param>
/// <param name="Column">The offending column; null when the problem is with the row as a whole.</param>
public sealed record EmployeeImportRowError(int RowNumber, string? EmployeeCode, string? Column, string Message);

public static class EmployeeImportLimits
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string XlsxExtension = ".xlsx";

    public const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>The spec's ceiling of 5,000 employees (§1.7) in one file.</summary>
    public const int MaxRows = 5_000;
}

/// <summary>
/// Upload checks that need no content (spec §2.6): extension and MIME type on a whitelist, and a size
/// cap. The reader then verifies the bytes really are an .xlsx workbook.
/// </summary>
internal sealed class ImportEmployeesCommandValidator : AbstractValidator<ImportEmployeesCommand>
{
    public ImportEmployeesCommandValidator()
    {
        RuleFor(c => c.File).NotNull();

        RuleFor(c => c.File.FileName)
            .Must(name => string.Equals(Path.GetExtension(name), EmployeeImportLimits.XlsxExtension, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Upload an Excel workbook (.xlsx).")
            .OverridePropertyName(nameof(ImportEmployeesCommand.File))
            .When(c => c.File is not null);

        RuleFor(c => c.File.ContentType)
            .Equal(EmployeeImportLimits.XlsxContentType, StringComparer.OrdinalIgnoreCase)
            .WithMessage($"The file must have content type {EmployeeImportLimits.XlsxContentType}.")
            .OverridePropertyName(nameof(ImportEmployeesCommand.File))
            .When(c => c.File is not null);

        RuleFor(c => c.File.Length)
            .InclusiveBetween(1, EmployeeImportLimits.MaxFileBytes)
            .WithMessage($"The file must be between 1 byte and {EmployeeImportLimits.MaxFileBytes / (1024 * 1024)} MB.")
            .OverridePropertyName(nameof(ImportEmployeesCommand.File))
            .When(c => c.File is not null);
    }
}

using Attendance.Shared.Results;

namespace Attendance.Application.Common.Files;

/// <summary>Reads tabular data out of an uploaded .xlsx workbook.</summary>
public interface ISpreadsheetReader
{
    /// <summary>
    /// Reads the first worksheet: its first row as the headers and every later non-blank row as data.
    /// Fails with <see cref="SpreadsheetErrors.Unreadable"/> when the content isn't a well-formed .xlsx
    /// workbook, whatever its file name claims, and with <see cref="SpreadsheetErrors.TooManyRows"/>
    /// beyond <paramref name="maxDataRows"/>.
    /// </summary>
    Task<Result<SpreadsheetTable>> ReadFirstSheetAsync(Stream content, int maxDataRows, CancellationToken cancellationToken);
}

/// <param name="Headers">The trimmed text of the first row.</param>
public sealed record SpreadsheetTable(IReadOnlyList<string> Headers, IReadOnlyList<SpreadsheetRow> Rows);

/// <summary>
/// One data row. <see cref="Cells"/> line up with the headers. Every value comes back as trimmed text,
/// null when blank: date cells as <c>yyyy-MM-dd</c>, numbers in the invariant culture.
/// </summary>
/// <param name="RowNumber">The worksheet row number, as the user sees it in Excel (the header is row 1).</param>
public sealed record SpreadsheetRow(int RowNumber, IReadOnlyList<string?> Cells)
{
    public string? this[int column] => column >= 0 && column < Cells.Count ? Cells[column] : null;

    // Cells hold personal data; keep them out of logs.
    public override string ToString() => $"{nameof(SpreadsheetRow)} {{ RowNumber = {RowNumber} }}";
}

public static class SpreadsheetErrors
{
    public static readonly Error Unreadable = new(
        "import.unreadable_file", "The file is not a readable .xlsx workbook.", ErrorType.Validation);

    public static Error TooManyRows(int max) =>
        new("import.too_many_rows", $"The file has more than {max} data rows. Split it into smaller files.", ErrorType.Validation);
}

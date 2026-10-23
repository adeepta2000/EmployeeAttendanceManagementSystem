using System.Globalization;
using System.IO.Compression;
using Attendance.Application.Common.Files;
using Attendance.Shared.Results;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace Attendance.Infrastructure.Files;

/// <summary>
/// Reads uploaded .xlsx workbooks with ClosedXML. The upload is checked before ClosedXML parses it: the
/// bytes must start with the ZIP signature every .xlsx has (spec §2.6: check magic bytes, not the file
/// name), and the archive must not declare more entries or uncompressed bytes than a real sheet needs.
/// Formulas are never evaluated; a formula cell yields the value Excel last saved for it.
/// </summary>
internal sealed class ClosedXmlSpreadsheetReader(ILogger<ClosedXmlSpreadsheetReader> logger) : ISpreadsheetReader
{
    // "PK\x03\x04": the local file header that starts every ZIP archive, and so every .xlsx.
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    // A few megabytes of ZIP can inflate to gigabytes of XML (a "zip bomb"). Real employee sheets
    // inflate to a few MB, so these leave plenty of headroom.
    private const long MaxUncompressedBytes = 100L * 1024 * 1024;
    private const int MaxArchiveEntries = 1_000;

    public async Task<Result<SpreadsheetTable>> ReadFirstSheetAsync(Stream content, int maxDataRows, CancellationToken cancellationToken)
    {
        // Upload size is capped before we get here; buffering gives ClosedXML the seekable stream it needs.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        if (!StartsWithZipSignature(buffer) || !IsWithinArchiveLimits(buffer))
        {
            logger.LogInformation("Rejected an upload that is not a plausible .xlsx archive ({Length} bytes)", buffer.Length);
            return SpreadsheetErrors.Unreadable;
        }

        try
        {
            buffer.Position = 0;
            using var workbook = new XLWorkbook(buffer);
            return Read(workbook.Worksheet(1), maxDataRows);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Malformed parts surface as many different exception types. The content is never logged.
            logger.LogInformation(ex, "Rejected an upload that ClosedXML could not read ({Length} bytes)", buffer.Length);
            return SpreadsheetErrors.Unreadable;
        }
    }

    private static Result<SpreadsheetTable> Read(IXLWorksheet sheet, int maxDataRows)
    {
        var headerRow = sheet.FirstRowUsed(XLCellsUsedOptions.Contents);
        if (headerRow is null)
        {
            return new SpreadsheetTable([], []);
        }

        var columnCount = headerRow.LastCellUsed(XLCellsUsedOptions.Contents)?.Address.ColumnNumber ?? 0;
        var headers = Enumerable.Range(1, columnCount)
            .Select(column => CellText(headerRow.Cell(column)) ?? string.Empty)
            .ToList();

        var rows = new List<SpreadsheetRow>();
        foreach (var row in sheet.RowsUsed(XLCellsUsedOptions.Contents))
        {
            if (row.RowNumber() <= headerRow.RowNumber())
            {
                continue;
            }

            var cells = Enumerable.Range(1, columnCount).Select(column => CellText(row.Cell(column))).ToList();
            if (cells.TrueForAll(cell => cell is null))
            {
                continue;
            }

            if (rows.Count == maxDataRows)
            {
                return SpreadsheetErrors.TooManyRows(maxDataRows);
            }

            rows.Add(new SpreadsheetRow(row.RowNumber(), cells));
        }

        return new SpreadsheetTable(headers, rows);
    }

    private static string? CellText(IXLCell cell)
    {
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        var text = value.Type switch
        {
            XLDataType.Blank => null,
            XLDataType.Text => value.GetText(),
            XLDataType.DateTime => DateOnly.FromDateTime(value.GetDateTime()).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            XLDataType.Number => value.GetNumber().ToString(CultureInfo.InvariantCulture),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static bool StartsWithZipSignature(MemoryStream buffer) =>
        buffer.Length >= ZipSignature.Length && buffer.GetBuffer().AsSpan(0, ZipSignature.Length).SequenceEqual(ZipSignature);

    private static bool IsWithinArchiveLimits(MemoryStream buffer)
    {
        try
        {
            buffer.Position = 0;
            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > MaxArchiveEntries)
            {
                return false;
            }

            long declared = 0;
            foreach (var entry in archive.Entries)
            {
                declared += entry.Length;
                if (declared > MaxUncompressedBytes)
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}

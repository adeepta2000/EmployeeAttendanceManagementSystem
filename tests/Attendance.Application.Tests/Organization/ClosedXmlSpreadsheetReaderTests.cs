using System.IO.Compression;
using Attendance.Application.Common.Files;
using Attendance.Infrastructure.Files;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Attendance.Application.Tests.Organization;

public sealed class ClosedXmlSpreadsheetReaderTests
{
    private readonly ClosedXmlSpreadsheetReader _reader = new(NullLogger<ClosedXmlSpreadsheetReader>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reads_the_header_row_and_every_non_blank_row_as_trimmed_text()
    {
        var file = Workbook(sheet =>
        {
            sheet.Cell("A1").Value = " EmployeeCode ";
            sheet.Cell("B1").Value = "JoiningDate";
            sheet.Cell("C1").Value = "Phone";
            sheet.Cell("D1").Value = "Note";

            sheet.Cell("A2").Value = "  NW-001 ";
            sheet.Cell("B2").Value = new DateTime(2024, 1, 15, 9, 30, 0);
            sheet.Cell("C2").Value = 1711000000;

            // Row 3 is formatted but empty, so it isn't data.
            sheet.Cell("A3").Style.Fill.BackgroundColor = XLColor.Yellow;

            sheet.Cell("A4").Value = "NW-002";
            sheet.Cell("B4").Value = "15/01/2024";
            sheet.Cell("D4").Value = 12.5;
        });

        var table = (await _reader.ReadFirstSheetAsync(file, maxDataRows: 10, Ct)).Value;

        table.Headers.Should().Equal("EmployeeCode", "JoiningDate", "Phone", "Note");
        table.Rows.Select(r => r.RowNumber).Should().Equal(2, 4);
        table.Rows[0].Cells.Should().Equal("NW-001", "2024-01-15", "1711000000", null);
        table.Rows[1].Cells.Should().Equal("NW-002", "15/01/2024", null, "12.5");
    }

    [Fact]
    public async Task Too_many_data_rows_fail_the_whole_file()
    {
        var file = Workbook(sheet =>
        {
            sheet.Cell("A1").Value = "EmployeeCode";
            for (var row = 2; row <= 4; row++)
            {
                sheet.Cell(row, 1).Value = $"NW-{row}";
            }
        });

        var result = await _reader.ReadFirstSheetAsync(file, maxDataRows: 2, Ct);

        result.Error!.Code.Should().Be("import.too_many_rows");
    }

    [Fact]
    public async Task Content_without_the_zip_signature_is_rejected_before_parsing()
    {
        var result = await _reader.ReadFirstSheetAsync(new MemoryStream("EmployeeCode,FirstName"u8.ToArray()), 10, Ct);

        result.Error.Should().Be(SpreadsheetErrors.Unreadable);
    }

    [Fact]
    public async Task A_zip_that_is_not_a_workbook_is_rejected()
    {
        var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using var entry = archive.CreateEntry("readme.txt").Open();
            await entry.WriteAsync("not a workbook"u8.ToArray(), Ct);
        }

        zip.Position = 0;
        var result = await _reader.ReadFirstSheetAsync(zip, 10, Ct);

        result.Error.Should().Be(SpreadsheetErrors.Unreadable);
    }

    [Fact]
    public async Task A_zip_that_declares_a_huge_uncompressed_size_is_rejected()
    {
        // Just over the 100 MB limit of zeros compresses to about 100 KB: the shape of a zip bomb.
        var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.SmallestSize).Open();
            var zeros = new byte[1024 * 1024];
            for (var i = 0; i < 101; i++)
            {
                await entry.WriteAsync(zeros, Ct);
            }
        }

        zip.Position = 0;
        var result = await _reader.ReadFirstSheetAsync(zip, 10, Ct);

        result.Error.Should().Be(SpreadsheetErrors.Unreadable);
    }

    private static MemoryStream Workbook(Action<IXLWorksheet> fill)
    {
        using var workbook = new XLWorkbook();
        fill(workbook.AddWorksheet("Employees"));
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}

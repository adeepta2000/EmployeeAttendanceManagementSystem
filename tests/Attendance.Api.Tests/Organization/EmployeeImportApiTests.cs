using System.Net;
using Attendance.Api.Tests.Infrastructure;
using Attendance.Application.Organization.Employees.Import;
using Attendance.Infrastructure.Persistence.Seeding;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Api.Tests.Organization;

/// <summary>Excel bulk import (FR-1.4): row-level errors, never a whole-file failure for a bad row.</summary>
[Collection(ApiCollection.Name)]
public sealed class EmployeeImportApiTests(AttendanceApiFactory api)
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] Headers =
    [
        "EmployeeCode", "First Name", "last_name", "DateOfBirth", "Gender", "Email", "JoiningDate", "EmploymentType",
        "DepartmentCode", "DesignationCode", "WorkLocationCode", "ReportingManagerCode",
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Valid_rows_are_imported_and_each_bad_row_is_reported_with_its_row_and_column()
    {
        var file = await MixedFileAsync();
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var result = await (await hr.PostFileAsync("/api/employees/import", file.Bytes, "staff.xlsx", XlsxType))
            .ReadAsync<EmployeeImportResult>();

        result.DryRun.Should().BeFalse();
        result.TotalRows.Should().Be(13, "the blank row 13 is skipped");
        result.ImportedRows.Should().Be(4);
        result.FailedRows.Should().Be(9);
        result.Errors.Select(e => (e.RowNumber, e.Column)).Should().Equal(
            (6, "DateOfBirth"),
            (7, "DepartmentCode"),
            (8, "EmployeeCode"),
            (9, "EmployeeCode"),
            (10, "EmployeeCode"),
            (11, "ReportingManagerCode"),
            (12, "ReportingManagerCode"),
            (14, "ReportingManagerCode"),
            (15, "ReportingManagerCode"));
        result.Errors.Single(e => e.RowNumber == 10).Message.Should().Contain("already exists");
        result.Errors.Single(e => e.RowNumber == 11).Message.Should().Contain("row 6");
        result.Errors.Single(e => e.RowNumber == 14).Message.Should().Contain("loop");

        var imported = await api.QueryDbAsync(db => db.Employees
            .Where(e => e.EmployeeCode.StartsWith(file.Prefix))
            .Select(e => new { e.EmployeeCode, Manager = e.ReportingManager!.EmployeeCode, e.Email, e.EmploymentType })
            .ToDictionaryAsync(e => e.EmployeeCode, Ct));
        imported.Keys.Should().BeEquivalentTo([file.Code("M"), file.Code("R1"), file.Code("R2"), file.Code("R3")]);
        imported[file.Code("M")].Manager.Should().Be(file.ExistingManagerCode, "a manager can be an existing employee");
        imported[file.Code("R1")].Manager.Should().Be(file.Code("M"), "or a row above in the same file");
        imported[file.Code("R2")].Manager.Should().Be(file.Code("R3"), "or a row below in the same file");
        imported[file.Code("M")].Email.Should().Be("m@nordwin.example");
    }

    [Fact]
    public async Task A_dry_run_reports_the_same_but_saves_nothing()
    {
        var file = await MixedFileAsync();
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var result = await (await hr.PostFileAsync("/api/employees/import?dryRun=true", file.Bytes, "staff.xlsx", XlsxType))
            .ReadAsync<EmployeeImportResult>();

        result.DryRun.Should().BeTrue();
        result.ImportedRows.Should().Be(4);
        result.Errors.Should().HaveCount(9);
        (await api.QueryDbAsync(db => db.Employees.CountAsync(e => e.EmployeeCode.StartsWith(file.Prefix), Ct))).Should().Be(0);
    }

    [Fact]
    public async Task The_file_must_be_an_xlsx_by_extension_and_content_type()
    {
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);
        var workbook = Workbook(Headers, []);

        var csv = await hr.PostFileAsync("/api/employees/import", workbook, "staff.csv", XlsxType);
        var wrongType = await hr.PostFileAsync("/api/employees/import", workbook, "staff.xlsx", "text/csv");

        (await csv.ReadValidationErrorKeysAsync()).Should().Equal("file");
        (await wrongType.ReadValidationErrorKeysAsync()).Should().Equal("file");
    }

    [Fact]
    public async Task Content_that_is_not_a_workbook_is_rejected_whatever_its_name()
    {
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var response = await hr.PostFileAsync("/api/employees/import", "EmployeeCode,FirstName\nNW-1,Rafiq"u8.ToArray(), "staff.xlsx", XlsxType);

        (await response.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("import.unreadable_file");
    }

    [Fact]
    public async Task A_file_over_the_size_cap_is_rejected()
    {
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var response = await hr.PostFileAsync(
            "/api/employees/import", new byte[EmployeeImportLimits.MaxFileBytes + 1], "staff.xlsx", XlsxType);

        (await response.ReadValidationErrorKeysAsync()).Should().Equal("file");
    }

    [Fact]
    public async Task Missing_required_columns_fail_the_file_and_are_named()
    {
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var response = await hr.PostFileAsync(
            "/api/employees/import", Workbook(["EmployeeCode", "FirstName"], [["NW-1", "Rafiq"]]), "staff.xlsx", XlsxType);

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        problem.Extensions["code"]!.ToString().Should().Be("import.missing_columns");
        problem.Detail.Should().Contain("LastName").And.Contain("WorkLocationCode").And.NotContain("ReportingManagerCode");
    }

    [Fact]
    public async Task A_header_row_with_no_data_rows_fails_the_file()
    {
        using var hr = await api.SignInAsync(SystemRoles.HROfficer);

        var response = await hr.PostFileAsync("/api/employees/import", Workbook(Headers, []), "staff.xlsx", XlsxType);

        (await response.ReadProblemCodeAsync(HttpStatusCode.BadRequest)).Should().Be("import.no_rows");
    }

    [Fact]
    public async Task Importing_needs_the_employee_import_permission()
    {
        using var manager = await api.SignInAsync(SystemRoles.Manager);

        var response = await manager.PostFileAsync("/api/employees/import", Workbook(Headers, []), "staff.xlsx", XlsxType);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Rows 2 to 5 are valid; every later row has one problem. Row 13 is blank. Codes carry a prefix
    /// unique to this file, since all tests share one database.
    /// </summary>
    private async Task<ImportFile> MixedFileAsync()
    {
        var org = await api.CreateOrgAsync();
        var existingManager = await api.CreateEmployeeAsync(org, firstName: "Farhana");
        var alreadyThere = await api.CreateEmployeeAsync(org);
        var prefix = $"IM{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        string Code(string suffix) => $"{prefix}-{suffix}";

        object?[] Row(string code, string? manager, object? dateOfBirth = null, string? department = null) =>
        [
            code, "Test", "Row", dateOfBirth ?? new DateTime(1995, 3, 14), "F", null, "2023-02-01", "Permanent",
            department ?? org.Department.Code, org.Designation.Code, org.WorkLocation.Code, manager,
        ];

        var bytes = Workbook(Headers,
        [
            [Code("M"), "Mitali", "Saha", "1988-11-02", "Female", "M@Nordwin.Example", new DateTime(2019, 6, 1), "permanent",
                org.Department.Code.ToLowerInvariant(), org.Designation.Code, org.WorkLocation.Code, existingManager.EmployeeCode],
            Row(Code("R1"), Code("M")),
            Row(Code("R2"), Code("R3")),
            Row(Code("R3"), Code("M").ToLowerInvariant()),
            Row(Code("BAD-DATE"), null, dateOfBirth: "31/31/1990"),
            Row(Code("BAD-DEPT"), null, department: "NO-SUCH-DEPT"),
            Row(Code("DUP"), null),
            Row(Code("DUP"), null),
            Row(alreadyThere.EmployeeCode, null),
            Row(Code("ORPHAN"), Code("BAD-DATE")),
            Row(Code("GHOST"), "NO-SUCH-PERSON"),
            null,
            Row(Code("L1"), Code("L2")),
            Row(Code("L2"), Code("L1")),
        ]);

        return new ImportFile(bytes, prefix, existingManager.EmployeeCode);
    }

    /// <param name="rows">One array per row from row 2 down; a null row is left blank.</param>
    private static byte[] Workbook(string[] headers, object?[]?[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Employees");
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }

        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < (rows[row]?.Length ?? 0); column++)
            {
                var cell = sheet.Cell(row + 2, column + 1);
                switch (rows[row]![column])
                {
                    case string text:
                        cell.Value = text;
                        break;
                    case DateTime date:
                        cell.Value = date;
                        break;
                }
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed record ImportFile(byte[] Bytes, string Prefix, string ExistingManagerCode)
    {
        public string Code(string suffix) => $"{Prefix}-{suffix}";
    }
}

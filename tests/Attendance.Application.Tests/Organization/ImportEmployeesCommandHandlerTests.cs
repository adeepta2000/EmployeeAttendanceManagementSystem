using Attendance.Application.Common.Files;
using Attendance.Application.Organization.Employees.Import;
using Attendance.Domain.Organization;
using Attendance.Infrastructure.Persistence;
using Attendance.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Attendance.Application.Tests.Organization;

/// <summary>
/// The import's row rules, with the spreadsheet already read (the reader has its own tests) and the
/// real repositories over SQLite. Rows are numbered from 2, as under a header row in Excel.
/// </summary>
public sealed class ImportEmployeesCommandHandlerTests : IAsyncLifetime
{
    private static readonly string[] Headers =
    [
        "EmployeeCode", "FirstName", "LastName", "DateOfBirth", "Gender", "Email", "Phone", "JoiningDate",
        "ConfirmationDate", "EmploymentType", "DepartmentCode", "DesignationCode", "WorkLocationCode", "ReportingManagerCode",
    ];

    private OrgHarness _org = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _org = await OrgHarness.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _org.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task A_failed_row_fails_every_row_below_it_in_an_in_file_reporting_chain()
    {
        // D reports to C, C to B, B to A, and A names a department that doesn't exist.
        var result = await ImportAsync(Headers,
            Row("D", manager: "C"),
            Row("C", manager: "B"),
            Row("B", manager: "A"),
            Row("A", department: "NOPE"));

        result.ImportedRows.Should().Be(0);
        result.Errors.Select(e => (e.RowNumber, e.Column)).Should().Equal(
            (2, "ReportingManagerCode"), (3, "ReportingManagerCode"), (4, "ReportingManagerCode"), (5, "DepartmentCode"));
        result.Errors[0].Message.Should().Contain("'C'").And.Contain("row 3");
    }

    [Fact]
    public async Task Headers_match_whatever_their_case_spacing_or_separators_and_extra_columns_are_ignored()
    {
        string[] headers =
        [
            "employee code", "FIRST_NAME", "Last-Name", "date of birth", "GENDER", "Joining Date", "employment type",
            "department code", "designation_code", "Work Location Code", "Remarks",
        ];

        var result = await ImportAsync(headers, ["nw-1", "Rafiq", "Islam", "1994-02-11", "m", "2021-07-01", "permanent", "sew", "opr", "dhk", "ok"]);

        result.Errors.Should().BeEmpty();
        await using var context = _org.CreateContext();
        var imported = await context.Employees.SingleAsync(Ct);
        (imported.EmployeeCode, imported.FullName, imported.Gender).Should().Be(("NW-1", "Rafiq Islam", Gender.Male));
    }

    [Theory]
    [InlineData("2024-01-15", true)]
    [InlineData("15/01/2024", true)]
    [InlineData("5/1/2024", true)]
    [InlineData("15-Jan-2024", true)]
    [InlineData("01/15/2024", false)]
    [InlineData("next Monday", false)]
    public async Task Text_dates_are_read_in_the_documented_formats(string joiningDate, bool accepted)
    {
        var result = await ImportAsync(Headers, Row("NW-1", joining: joiningDate));

        result.ImportedRows.Should().Be(accepted ? 1 : 0);
        if (!accepted)
        {
            result.Errors.Should().ContainSingle().Which.Column.Should().Be("JoiningDate");
        }
    }

    [Theory]
    [InlineData("m", true)]
    [InlineData("FEMALE", true)]
    [InlineData("Other", true)]
    [InlineData("1", false)]
    [InlineData("X", false)]
    public async Task Gender_takes_names_or_a_single_letter_but_never_a_number(string gender, bool accepted)
    {
        var result = await ImportAsync(Headers, Row("NW-1", gender: gender));

        result.ImportedRows.Should().Be(accepted ? 1 : 0);
    }

    [Fact]
    public async Task A_manager_outside_the_callers_scope_reads_as_not_found()
    {
        var boss = await _org.AddEmployeeAsync("BOSS");
        await _org.AddEmployeeAsync("STRANGER");
        var retired = await _org.AddEmployeeAsync("RETIRED", boss, isActive: false);
        _org.Caller.RestrictTo(self: boss.Id, retired.Id);

        var result = await ImportAsync(Headers,
            Row("NW-1", manager: "BOSS"),
            Row("NW-2", manager: "STRANGER"),
            Row("NW-3", manager: "RETIRED"));

        result.ImportedRows.Should().Be(1);
        result.Errors.Select(e => e.RowNumber).Should().Equal(3, 4);
        result.Errors.Should().OnlyContain(e => e.Message.Contains("not found or is inactive"));

        await using var context = _org.CreateContext();
        (await context.Employees.SingleAsync(e => e.EmployeeCode == "NW-1", Ct)).ReportingManagerId.Should().Be(boss.Id);
    }

    [Fact]
    public async Task A_retired_department_takes_no_imported_employees()
    {
        await _org.RetireDepartmentAsync();

        var result = await ImportAsync(Headers, Row("NW-1"));

        result.Errors.Should().ContainSingle().Which.Column.Should().Be("DepartmentCode");
    }

    [Fact]
    public async Task An_employee_cannot_report_to_themselves()
    {
        var result = await ImportAsync(Headers, Row("NW-1", manager: "nw-1"));

        result.Errors.Should().ContainSingle().Which.Message.Should().Contain("themselves");
    }

    [Fact]
    public async Task The_same_rules_as_the_create_command_apply()
    {
        var result = await ImportAsync(Headers,
            Row("NW-1", dob: "2015-06-01"),
            ["NW 2", "", "Row", "1990-01-01", "Male", "not-an-email", null, "2023-02-01", "2022-01-01", "Permanent", "SEW", "OPR", "DHK", null]);

        result.Errors.Select(e => (e.RowNumber, e.Column)).Should().BeEquivalentTo(
        [
            (2, "DateOfBirth"),
            (3, "EmployeeCode"), (3, "FirstName"), (3, "Email"), (3, "ConfirmationDate"),
        ]);
    }

    private async Task<EmployeeImportResult> ImportAsync(string[] headers, params string?[][] rows)
    {
        var table = new SpreadsheetTable(headers, [.. rows.Select((cells, i) => new SpreadsheetRow(i + 2, cells))]);

        await using var context = _org.CreateContext();
        var handler = new ImportEmployeesCommandHandler(
            new FakeReader(table),
            OrgHarness.Employees(context),
            OrgHarness.Departments(context),
            OrgHarness.MasterData<Designation>(context),
            OrgHarness.MasterData<WorkLocation>(context),
            _org.Scopes(),
            new EmployeeImportRowValidator(),
            new UnitOfWork(context),
            NullLogger<ImportEmployeesCommandHandler>.Instance);

        var upload = new UploadedFile("staff.xlsx", EmployeeImportLimits.XlsxContentType, 1, Stream.Null);
        var result = await handler.HandleAsync(new ImportEmployeesCommand(upload, DryRun: false), Ct);
        return result.Value;
    }

    private static string?[] Row(
        string code,
        string? manager = null,
        string dob = "1995-03-14",
        string gender = "Female",
        string department = "SEW",
        string joining = "2023-02-01") =>
        [code, "Test", "Row", dob, gender, null, null, joining, null, "Permanent", department, "OPR", "DHK", manager];

    private sealed class FakeReader(SpreadsheetTable table) : ISpreadsheetReader
    {
        public Task<Result<SpreadsheetTable>> ReadFirstSheetAsync(Stream content, int maxDataRows, CancellationToken cancellationToken) =>
            Task.FromResult<Result<SpreadsheetTable>>(table);
    }
}

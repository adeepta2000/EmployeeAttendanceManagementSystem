using System.Globalization;
using Attendance.Application.Common.Files;
using Attendance.Domain.Organization;
using Attendance.Shared.Results;
using FluentValidation;

namespace Attendance.Application.Organization.Employees.Import;

/// <summary>The import template's columns. Headers match ignoring case, spaces, underscores and hyphens.</summary>
internal static class EmployeeImportColumns
{
    public const string EmployeeCode = nameof(EmployeeCode);
    public const string FirstName = nameof(FirstName);
    public const string LastName = nameof(LastName);
    public const string DateOfBirth = nameof(DateOfBirth);
    public const string Gender = nameof(Gender);
    public const string Email = nameof(Email);
    public const string Phone = nameof(Phone);
    public const string JoiningDate = nameof(JoiningDate);
    public const string ConfirmationDate = nameof(ConfirmationDate);
    public const string EmploymentType = nameof(EmploymentType);
    public const string DepartmentCode = nameof(DepartmentCode);
    public const string DesignationCode = nameof(DesignationCode);
    public const string WorkLocationCode = nameof(WorkLocationCode);
    public const string ReportingManagerCode = nameof(ReportingManagerCode);

    /// <summary>Must be in the header row.</summary>
    public static readonly string[] Required =
    [
        EmployeeCode, FirstName, LastName, DateOfBirth, Gender, JoiningDate, EmploymentType,
        DepartmentCode, DesignationCode, WorkLocationCode,
    ];

    /// <summary>May be left out of the header row altogether.</summary>
    public static readonly string[] Optional = [Email, Phone, ConfirmationDate, ReportingManagerCode];
}

/// <summary>Where each template column sits in a particular file.</summary>
internal sealed class EmployeeImportLayout
{
    private readonly Dictionary<string, int> _columns;

    private EmployeeImportLayout(Dictionary<string, int> columns) => _columns = columns;

    public static Result<EmployeeImportLayout> Create(IReadOnlyList<string> headers)
    {
        var byKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            // The first of two identically named columns wins; unknown columns are ignored.
            byKey.TryAdd(Key(headers[i]), i);
        }

        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var column in EmployeeImportColumns.Required.Concat(EmployeeImportColumns.Optional))
        {
            if (byKey.TryGetValue(Key(column), out var index))
            {
                columns[column] = index;
            }
        }

        var missing = EmployeeImportColumns.Required.Where(c => !columns.ContainsKey(c)).ToList();
        return missing.Count > 0
            ? EmployeeImportErrors.MissingColumns(missing)
            : new EmployeeImportLayout(columns);
    }

    public string? Get(SpreadsheetRow row, string column) => _columns.TryGetValue(column, out var index) ? row[index] : null;

    private static string Key(string header) =>
        string.Concat(header.Where(c => !char.IsWhiteSpace(c) && c is not '_' and not '-'));
}

/// <summary>
/// A data row converted to typed values. A cell that couldn't be converted is left at its default and
/// reported by <see cref="EmployeeImportRowParser"/>; the validator's complaint about that column is dropped.
/// </summary>
internal sealed record EmployeeImportRow(
    string EmployeeCode,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? Email,
    string? Phone,
    DateOnly JoiningDate,
    DateOnly? ConfirmationDate,
    EmploymentType EmploymentType,
    string DepartmentCode,
    string DesignationCode,
    string WorkLocationCode,
    string? ReportingManagerCode)
    : IEmployeeFields
{
    public override string ToString() => nameof(EmployeeImportRow);
}

/// <summary>The same field rules as the create command, plus the code columns that stand in for PublicIds.</summary>
internal sealed class EmployeeImportRowValidator : AbstractValidator<EmployeeImportRow>
{
    public EmployeeImportRowValidator()
    {
        this.AddEmployeeFieldRules();
        RuleFor(r => r.DepartmentCode).MustBeCode(Department.CodeMaxLength);
        RuleFor(r => r.DesignationCode).MustBeCode(Designation.CodeMaxLength);
        RuleFor(r => r.WorkLocationCode).MustBeCode(WorkLocation.CodeMaxLength);
        RuleFor(r => r.ReportingManagerCode!).MustBeCode(Employee.EmployeeCodeMaxLength)
            .When(r => r.ReportingManagerCode is not null);
    }
}

internal static class EmployeeImportRowParser
{
    /// <summary>Text dates are read in these formats; real date cells always work.</summary>
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MMM-yyyy", "d-MMM-yyyy"];

    private const string DateHint = "Use a date cell, or text as yyyy-MM-dd or dd/MM/yyyy.";

    private const int MaxEchoedLength = 40;

    /// <summary>Converts and validates one row. Returns the typed row and a (column, message) per problem.</summary>
    public static (EmployeeImportRow Row, List<(string Column, string Message)> Errors) Parse(
        SpreadsheetRow sheetRow, EmployeeImportLayout layout, IValidator<EmployeeImportRow> validator)
    {
        var errors = new List<(string Column, string Message)>();
        string? Text(string column) => layout.Get(sheetRow, column);

        DateOnly? Date(string column, bool required)
        {
            var text = Text(column);
            if (text is null)
            {
                if (required)
                {
                    errors.Add((column, $"{column} is required."));
                }

                return null;
            }

            if (DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            errors.Add((column, $"'{Echo(text)}' is not a date. {DateHint}"));
            return null;
        }

        TEnum Choice<TEnum>(string column, IReadOnlyDictionary<string, TEnum> accepted)
            where TEnum : struct, Enum
        {
            var text = Text(column);
            if (text is not null && accepted.TryGetValue(text, out var value))
            {
                return value;
            }

            errors.Add((column, text is null
                ? $"{column} is required."
                : $"'{Echo(text)}' is not one of: {string.Join(", ", Enum.GetNames<TEnum>())}."));
            return default;
        }

        var row = new EmployeeImportRow(
            EmployeeCode: Text(EmployeeImportColumns.EmployeeCode) ?? string.Empty,
            FirstName: Text(EmployeeImportColumns.FirstName) ?? string.Empty,
            LastName: Text(EmployeeImportColumns.LastName) ?? string.Empty,
            DateOfBirth: Date(EmployeeImportColumns.DateOfBirth, required: true) ?? default,
            Gender: Choice(EmployeeImportColumns.Gender, Genders),
            Email: Text(EmployeeImportColumns.Email),
            Phone: Text(EmployeeImportColumns.Phone),
            JoiningDate: Date(EmployeeImportColumns.JoiningDate, required: true) ?? default,
            ConfirmationDate: Date(EmployeeImportColumns.ConfirmationDate, required: false),
            EmploymentType: Choice(EmployeeImportColumns.EmploymentType, EmploymentTypes),
            DepartmentCode: Text(EmployeeImportColumns.DepartmentCode) ?? string.Empty,
            DesignationCode: Text(EmployeeImportColumns.DesignationCode) ?? string.Empty,
            WorkLocationCode: Text(EmployeeImportColumns.WorkLocationCode) ?? string.Empty,
            ReportingManagerCode: Text(EmployeeImportColumns.ReportingManagerCode));

        // A cell that failed conversion was reported above; its default value would only add noise.
        var unconvertible = errors.Select(e => e.Column).ToHashSet(StringComparer.Ordinal);
        errors.AddRange(validator.Validate(row).Errors
            .Where(failure => !unconvertible.Contains(failure.PropertyName))
            .Select(failure => (failure.PropertyName, failure.ErrorMessage)));

        return (row, errors);
    }

    // Enum names, case-insensitive, plus the usual single-letter shorthands. Numbers are refused: "1"
    // is not obviously "Male" to whoever filled in the sheet.
    private static readonly Dictionary<string, Gender> Genders = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(Gender.Male)] = Gender.Male,
        ["M"] = Gender.Male,
        [nameof(Gender.Female)] = Gender.Female,
        ["F"] = Gender.Female,
        [nameof(Gender.Other)] = Gender.Other,
    };

    private static readonly Dictionary<string, EmploymentType> EmploymentTypes =
        Enum.GetValues<EmploymentType>().ToDictionary(t => t.ToString(), StringComparer.OrdinalIgnoreCase);

    private static string Echo(string text) => text.Length <= MaxEchoedLength ? text : text[..MaxEchoedLength] + "…";
}

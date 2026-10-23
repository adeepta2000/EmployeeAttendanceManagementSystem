using System.Text.RegularExpressions;
using FluentValidation;

namespace Attendance.Application.Organization;

/// <summary>
/// Business codes such as <c>SEW</c> or <c>NW-001</c>. They are stored trimmed and upper-case, so
/// uniqueness never depends on how a code was typed.
/// </summary>
internal static partial class OrgCodes
{
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public static bool IsWellFormed(string? code, int maxLength)
    {
        var trimmed = code?.Trim();
        return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= maxLength && Pattern().IsMatch(trimmed);
    }

    /// <summary>Trims, and turns a blank optional value into null.</summary>
    public static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Letters, digits and - _ . /, starting with a letter or digit.
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_./-]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Pattern();
}

internal static class OrgValidationRules
{
    public static IRuleBuilderOptions<T, string> MustBeCode<T>(this IRuleBuilder<T, string> rule, int maxLength) =>
        rule.NotEmpty()
            .Must(code => OrgCodes.IsWellFormed(code, maxLength))
            .WithMessage($"'{{PropertyName}}' must be at most {maxLength} letters, digits or - _ . / characters, starting with a letter or digit.");

    public static IRuleBuilderOptions<T, string> MustBeName<T>(this IRuleBuilder<T, string> rule, int maxLength) =>
        rule.NotEmpty().Must(name => name.Trim().Length <= maxLength)
            .WithMessage($"'{{PropertyName}}' must be {maxLength} characters or fewer.");
}

namespace Attendance.Application.Identity;

/// <summary>
/// Claim names in the access token. They are read as-is (inbound claim mapping is off), so a claim
/// is named the same when issued and when checked.
/// </summary>
public static class AuthClaimTypes
{
    /// <summary>The user's PublicId.</summary>
    public const string Subject = "sub";

    /// <summary>The linked employee's PublicId; absent for an account with no employee record.</summary>
    public const string EmployeeId = "employee_id";

    /// <summary>One claim per permission code (FR-8.2). Roles are never put in the token.</summary>
    public const string Permission = "permission";

    /// <summary>
    /// Present (value "true") while the account must change its password (screen 4). Such a token can
    /// call nothing but the change-password endpoint.
    /// </summary>
    public const string PasswordChangeRequired = "pwd_change";
}

namespace Attendance.Shared.Results;

/// <summary>
/// An expected failure of a command or query. <see cref="Code"/> is stable and machine-readable
/// (e.g. <c>auth.invalid_credentials</c>); <see cref="Message"/> is safe to show to the caller.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type);

/// <summary>The kind of failure. The API maps each one to an HTTP status code.</summary>
public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
}

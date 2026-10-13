namespace Attendance.Application.Common.Interfaces;

/// <summary>
/// The caller behind the current request. It reads claims only, with no database access, so the
/// persistence layer can depend on it without a cycle.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The signed-in user's PublicId (the token subject), or null when nobody is signed in.</summary>
    string? UserId { get; }

    /// <summary>The permission codes in the caller's access token; empty when nobody is signed in.</summary>
    IReadOnlyCollection<string> Permissions { get; }
}

namespace Attendance.Application.Common.Exceptions;

/// <summary>
/// The request has no active signed-in user behind it, e.g. the account was deactivated after its
/// access token was issued. The API answers 401 so the client signs in again.
/// </summary>
public sealed class UnauthenticatedException()
    : Exception("The request has no active signed-in user.");

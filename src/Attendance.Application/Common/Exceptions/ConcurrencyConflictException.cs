namespace Attendance.Application.Common.Exceptions;

/// <summary>
/// A save lost a race: a row it updates was changed by another request after it was read. Nothing
/// from the failed save was written.
/// </summary>
public sealed class ConcurrencyConflictException(string message, Exception innerException)
    : Exception(message, innerException);

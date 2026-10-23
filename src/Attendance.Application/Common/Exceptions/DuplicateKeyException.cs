namespace Attendance.Application.Common.Exceptions;

/// <summary>
/// A save broke a unique index, e.g. two requests created the same code at once and both passed the
/// handler's existence check. Nothing from the failed save was written. The API answers 409.
/// </summary>
public sealed class DuplicateKeyException(string message, Exception innerException)
    : Exception(message, innerException);

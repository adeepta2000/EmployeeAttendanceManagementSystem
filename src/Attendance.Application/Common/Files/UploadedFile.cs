namespace Attendance.Application.Common.Files;

/// <summary>
/// A file uploaded by the caller, as received. Name, content type and length are client-supplied and
/// untrusted: validators check them, and readers still verify the content itself (spec §2.6).
/// </summary>
public sealed record UploadedFile(string FileName, string? ContentType, long Length, Stream Content)
{
    public override string ToString() => $"{nameof(UploadedFile)} {{ Length = {Length} }}";
}

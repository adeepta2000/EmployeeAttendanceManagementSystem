namespace Attendance.Domain.Organization;

/// <summary>Persisted as TINYINT, so never renumber existing members. Zero is deliberately unused.</summary>
public enum Gender : byte
{
    Male = 1,
    Female = 2,
    Other = 3,
}

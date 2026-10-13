namespace Attendance.Domain.Organization;

/// <summary>FR-1.2. Persisted as TINYINT, so never renumber existing members. Zero is deliberately unused.</summary>
public enum EmploymentType : byte
{
    Permanent = 1,
    Contract = 2,
    Probation = 3,
    Intern = 4,
}

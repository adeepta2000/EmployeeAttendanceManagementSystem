namespace Attendance.Domain.Identity;

/// <summary>
/// The permission catalog (FR-8.2): every permission code, defined once. Authorization policies, seed
/// data and the client's mirror module all use these constants; nothing checks a role name.
/// </summary>
/// <remarks>
/// FR-8.2 names <c>attendance.process</c>, <c>leave.approve</c>, <c>report.export</c> and
/// <c>employee.write</c>; the rest are derived from the functional requirements cited in each description.
/// Codes are persisted and embedded in access tokens, so never rename one: add a new code instead.
/// </remarks>
public static class Permissions
{
    public static class Org
    {
        public const string Write = "org.write";
    }

    public static class Employee
    {
        public const string Read = "employee.read";
        public const string Write = "employee.write";
        public const string Import = "employee.import";
    }

    /// <summary>Data scope (FR-8.3). Without <see cref="Company"/>, a caller sees only themselves and their reporting tree.</summary>
    public static class Scope
    {
        public const string Company = "scope.company";
    }

    public static class Shift
    {
        public const string Write = "shift.write";
    }

    public static class Roster
    {
        public const string Write = "roster.write";
    }

    public static class Holiday
    {
        public const string Write = "holiday.write";
    }

    public static class Attendance
    {
        public const string Punch = "attendance.punch";
        public const string Import = "attendance.import";
        public const string Process = "attendance.process";
    }

    public static class Regularization
    {
        public const string Request = "regularization.request";
        public const string Approve = "regularization.approve";
    }

    public static class Period
    {
        public const string Lock = "period.lock";
        public const string Unlock = "period.unlock";
    }

    public static class Leave
    {
        public const string Apply = "leave.apply";
        public const string Approve = "leave.approve";
        public const string Configure = "leave.configure";
    }

    public static class Overtime
    {
        public const string Authorize = "overtime.authorize";
        public const string Approve = "overtime.approve";
        public const string Override = "overtime.override";
        public const string Configure = "overtime.configure";
    }

    public static class Report
    {
        public const string View = "report.view";
        public const string Export = "report.export";
    }

    public static class User
    {
        public const string Manage = "user.manage";
    }

    public static class Audit
    {
        public const string Read = "audit.read";
    }

    public static class Settings
    {
        public const string Write = "settings.write";
    }

    /// <summary>Every permission with its module (§2.1) and description, in display order.</summary>
    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(Org.Write, Modules.Org, "Maintain departments, designations and work locations (FR-1.1)"),
        new(Employee.Read, Modules.Org, "View employee records within the caller's data scope (FR-1.2, FR-8.3)"),
        new(Employee.Write, Modules.Org, "Create, edit and deactivate employees (FR-1.2, FR-1.5)"),
        new(Employee.Import, Modules.Org, "Bulk-import employees from Excel (FR-1.4)"),
        new(Scope.Company, Modules.Org, "See data for the whole company, not just self and reporting tree (FR-8.3)"),

        new(Shift.Write, Modules.Shift, "Define shifts and weekly-off patterns (FR-2.1, FR-2.2)"),
        new(Roster.Write, Modules.Shift, "Assign default shifts and roster overrides (FR-2.3, FR-2.4)"),
        new(Holiday.Write, Modules.Shift, "Maintain the public-holiday calendar (FR-7.1)"),

        new(Attendance.Punch, Modules.Attendance, "Punch in and out from web or mobile (FR-3.1)"),
        new(Attendance.Import, Modules.Attendance, "Import punches from a biometric device export (FR-3.3)"),
        new(Attendance.Process, Modules.Attendance, "Run attendance processing for a date range (FR-4.4)"),
        new(Regularization.Request, Modules.Attendance, "Raise a regularization request for a missed or wrong punch (FR-3.5)"),
        new(Regularization.Approve, Modules.Attendance, "Approve or reject regularization requests (FR-3.5)"),
        new(Period.Lock, Modules.Attendance, "Lock an attendance month (FR-10.1)"),
        new(Period.Unlock, Modules.Attendance, "Unlock a locked attendance month, with a reason (FR-10.2)"),

        new(Leave.Apply, Modules.Leave, "Apply for and cancel own leave (FR-6.3, FR-6.7)"),
        new(Leave.Approve, Modules.Leave, "Approve or reject leave applications (FR-6.5)"),
        new(Leave.Configure, Modules.Leave, "Define leave types and adjust leave balances (FR-6.1, FR-6.2)"),

        new(Overtime.Authorize, Modules.Overtime, "Pre-authorize overtime before the day (FR-5.5)"),
        new(Overtime.Approve, Modules.Overtime, "Approve or reject overtime records (FR-5.6)"),
        new(Overtime.Override, Modules.Overtime, "Release a cap-exceeding overtime record, with a reason (BR-07)"),
        new(Overtime.Configure, Modules.Overtime, "Define overtime rules, thresholds and caps (FR-5.1 to FR-5.4)"),

        new(Report.View, Modules.Reporting, "View attendance, overtime and leave reports (FR-9.1 to FR-9.6)"),
        new(Report.Export, Modules.Reporting, "Export reports to Excel and PDF (FR-9.7)"),

        new(User.Manage, Modules.Identity, "Manage user accounts and role permissions (FR-8.1, FR-8.2)"),
        new(Audit.Read, Modules.Identity, "Read the audit log (FR-12.2)"),
        new(Settings.Write, Modules.Identity, "Change system settings: grace defaults, timezone, notifications"),
    ];

    /// <summary>The application modules of spec §2.1, as stored in <see cref="Permission.Module"/>.</summary>
    public static class Modules
    {
        public const string Org = "Org";
        public const string Shift = "Shift";
        public const string Attendance = "Attendance";
        public const string Leave = "Leave";
        public const string Overtime = "OT";
        public const string Reporting = "Reporting";
        public const string Identity = "Identity";
    }
}

/// <summary>One entry of the <see cref="Permissions"/> catalog.</summary>
public sealed record PermissionDefinition(string Code, string Module, string Description);

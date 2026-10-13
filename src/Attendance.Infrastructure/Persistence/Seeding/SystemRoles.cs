using Attendance.Domain.Identity;

namespace Attendance.Infrastructure.Persistence.Seeding;

/// <summary>
/// The built-in roles of FR-8.1 and the permissions each one starts with. Role names appear only here
/// and in seed data: authorization always checks permissions (FR-8.2).
/// </summary>
public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string HRManager = "HRManager";
    public const string HROfficer = "HROfficer";
    public const string Manager = "Manager";
    public const string Employee = "Employee";

    private static readonly string[] SelfService =
    [
        Permissions.Attendance.Punch,
        Permissions.Regularization.Request,
        Permissions.Leave.Apply,
    ];

    private static readonly string[] HROfficerPermissions =
    [
        .. SelfService,
        Permissions.Employee.Read,
        Permissions.Employee.Write,
        Permissions.Employee.Import,
        Permissions.Scope.Company,
        Permissions.Roster.Write,
        Permissions.Attendance.Import,
        Permissions.Attendance.Process,
        Permissions.Regularization.Approve,
        Permissions.Period.Lock,
        Permissions.Leave.Approve,
        Permissions.Overtime.Approve,
        Permissions.Overtime.Override,
        Permissions.Report.View,
        Permissions.Report.Export,
    ];

    public static IReadOnlyList<SystemRole> All { get; } =
    [
        // The IT admin persona of §1.2: accounts, device integration, audit review. Not every
        // permission: unlocking a period is for HRManager alone (FR-10.2).
        new(Admin, "IT administration: user accounts, roles, device imports and the audit log",
        [
            Permissions.User.Manage,
            Permissions.Settings.Write,
            Permissions.Audit.Read,
            Permissions.Attendance.Import,
            Permissions.Employee.Read,
            Permissions.Scope.Company,
        ]),

        new(HRManager, "HR policy owner: shifts, OT rules, leave policy, holidays and period unlock",
        [
            .. HROfficerPermissions,
            Permissions.Org.Write,
            Permissions.Shift.Write,
            Permissions.Holiday.Write,
            Permissions.Period.Unlock,
            Permissions.Leave.Configure,
            Permissions.Overtime.Configure,
            Permissions.Audit.Read,
        ]),

        new(HROfficer, "HR operations: processing, regularizations, period lock and payroll exports", HROfficerPermissions),

        new(Manager, "Line manager: approvals and rosters for their reporting tree",
        [
            .. SelfService,
            Permissions.Employee.Read,
            Permissions.Roster.Write,
            Permissions.Regularization.Approve,
            Permissions.Leave.Approve,
            Permissions.Overtime.Authorize,
            Permissions.Overtime.Approve,
            Permissions.Report.View,
        ]),

        new(Employee, "Self-service: punch, leave and regularization requests", SelfService),
    ];
}

public sealed record SystemRole(string Name, string Description, IReadOnlyList<string> DefaultPermissions);

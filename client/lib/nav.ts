import { Permissions, satisfies, type PermissionRule } from "@/lib/permissions"

// The sidebar (spec §4.3): grouped by task, not by database table, and filtered by permission on the
// server, so a page never even contains links its user can't follow. An Employee sees four items; HR and
// admins see more.
//
// `available: false` marks screens of later phases: they show, disabled, to the people who will get them,
// so the menu doesn't reshuffle as modules land. Flip the flag when the screen ships.

export type NavItem = {
  id: string
  href: string
  rule?: PermissionRule
  available: boolean
  /** Other path prefixes that highlight this item. */
  matches?: string[]
}

export type NavGroup = { id: string; items: NavItem[] }

const P = Permissions

export const navigation: NavGroup[] = [
  {
    id: "home",
    items: [{ id: "dashboard", href: "/dashboard", available: false }],
  },
  {
    id: "attendance",
    items: [
      { id: "myAttendance", href: "/attendance/me", rule: { all: [P.Attendance.Punch] }, available: false },
      { id: "register", href: "/attendance/register", rule: { all: [P.Report.View] }, available: false },
      { id: "musterRoll", href: "/attendance/muster", rule: { all: [P.Report.View] }, available: false },
      { id: "processing", href: "/attendance/processing", rule: { all: [P.Attendance.Process] }, available: false },
      { id: "regularizations", href: "/attendance/regularizations", rule: { all: [P.Regularization.Approve] }, available: false },
      { id: "punchImport", href: "/attendance/import", rule: { all: [P.Attendance.Import] }, available: false },
      { id: "periodLock", href: "/attendance/periods", rule: { all: [P.Period.Lock] }, available: false },
    ],
  },
  {
    id: "approvals",
    items: [
      {
        id: "approvals",
        href: "/approvals",
        rule: { any: [P.Leave.Approve, P.Overtime.Approve, P.Regularization.Approve] },
        available: false,
      },
    ],
  },
  {
    id: "leave",
    items: [
      { id: "myLeave", href: "/leave/me", rule: { all: [P.Leave.Apply] }, available: false },
      { id: "leaveLedger", href: "/leave/ledger", rule: { all: [P.Scope.Company, P.Leave.Approve] }, available: false },
    ],
  },
  {
    id: "overtime",
    items: [
      { id: "myOvertime", href: "/overtime/me", rule: { all: [P.Attendance.Punch] }, available: false },
      { id: "otAuthorization", href: "/overtime/authorize", rule: { all: [P.Overtime.Authorize] }, available: false },
      { id: "otReview", href: "/overtime/review", rule: { all: [P.Overtime.Override] }, available: false },
    ],
  },
  {
    id: "reports",
    items: [{ id: "reports", href: "/reports", rule: { all: [P.Report.View] }, available: false }],
  },
  {
    id: "setup",
    items: [
      // People beyond yourself: HR and admins (company scope) or anyone with a team to approve for.
      {
        id: "employees",
        href: "/employees",
        rule: { all: [P.Employee.Read], any: [P.Scope.Company, P.Employee.Write, P.Leave.Approve] },
        available: true,
      },
      {
        id: "orgStructure",
        href: "/departments",
        matches: ["/designations"],
        rule: { any: [P.Org.Write, P.Scope.Company] },
        available: true,
      },
      { id: "shifts", href: "/setup/shifts", rule: { all: [P.Shift.Write] }, available: true },
      { id: "weeklyOffs", href: "/setup/weekly-offs", rule: { all: [P.Shift.Write] }, available: true },
      { id: "rosters", href: "/setup/rosters", rule: { all: [P.Roster.Write] }, available: true },
      { id: "leaveTypes", href: "/setup/leave-types", rule: { all: [P.Leave.Configure] }, available: false },
      { id: "holidays", href: "/setup/holidays", rule: { all: [P.Holiday.Write] }, available: true },
    ],
  },
  {
    id: "admin",
    items: [
      { id: "users", href: "/admin/users", rule: { all: [P.User.Manage] }, available: false },
      { id: "auditLog", href: "/admin/audit", rule: { all: [P.Audit.Read] }, available: false },
      { id: "settings", href: "/admin/settings", rule: { all: [P.Settings.Write] }, available: false },
    ],
  },
]

/** The menu for a set of permissions: groups with at least one visible item. */
export function navigationFor(permissions: Iterable<string>): NavGroup[] {
  const granted = new Set(permissions)
  return navigation
    .map((group) => ({ ...group, items: group.items.filter((item) => satisfies(granted, item.rule)) }))
    .filter((group) => group.items.length > 0)
}

export function isActive(item: NavItem, pathname: string): boolean {
  return [item.href, ...(item.matches ?? [])].some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`))
}

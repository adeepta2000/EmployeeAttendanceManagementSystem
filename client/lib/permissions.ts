/**
 * Mirror of the API's permission catalog (src/Attendance.Domain/Identity/Permissions.cs). Codes are
 * persisted and embedded in access tokens, so they are never renamed: when the API adds one, add it here.
 *
 * The client uses permissions only to decide what to show. Hiding a button is not authorization: the API
 * checks every request again (spec §2.5).
 */
export const Permissions = {
  Org: { Write: "org.write" },
  Employee: { Read: "employee.read", Write: "employee.write", Import: "employee.import" },
  Scope: { Company: "scope.company" },
  Shift: { Write: "shift.write" },
  Roster: { Write: "roster.write" },
  Holiday: { Write: "holiday.write" },
  Attendance: { Punch: "attendance.punch", Import: "attendance.import", Process: "attendance.process" },
  Regularization: { Request: "regularization.request", Approve: "regularization.approve" },
  Period: { Lock: "period.lock", Unlock: "period.unlock" },
  Leave: { Apply: "leave.apply", Approve: "leave.approve", Configure: "leave.configure" },
  Overtime: {
    Authorize: "overtime.authorize",
    Approve: "overtime.approve",
    Override: "overtime.override",
    Configure: "overtime.configure",
  },
  Report: { View: "report.view", Export: "report.export" },
  User: { Manage: "user.manage" },
  Audit: { Read: "audit.read" },
  Settings: { Write: "settings.write" },
} as const

type Leaves<T> = T extends string ? T : { [K in keyof T]: Leaves<T[K]> }[keyof T]

export type Permission = Leaves<typeof Permissions>

/** Every permission must be held (`all`) and at least one of `any`, when given. */
export type PermissionRule = { all?: readonly Permission[]; any?: readonly Permission[] }

export function satisfies(granted: ReadonlySet<string>, rule: PermissionRule | undefined): boolean {
  if (!rule) return true
  if (rule.all && !rule.all.every((p) => granted.has(p))) return false
  if (rule.any && !rule.any.some((p) => granted.has(p))) return false
  return true
}

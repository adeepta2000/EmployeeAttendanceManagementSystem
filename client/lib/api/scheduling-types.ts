import type { EmployeeRef, OrgUnitRef } from "./types"

// Response shapes of the Scheduling module (src/Attendance.Application/Scheduling/SchedulingDtos.cs).
// Times are Asia/Dhaka wall-clock `HH:mm:ss`, dates `yyyy-MM-dd`, and enums travel as their names.

/** System.DayOfWeek, in its own order: Sunday is 0. */
export const DaysOfWeek = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"] as const
export type DayOfWeek = (typeof DaysOfWeek)[number]

export const DayTypes = ["Working", "WeeklyOff", "Holiday"] as const
export type DayType = (typeof DayTypes)[number]

/** A month has at most five of any weekday (WeeklyOffPattern.MaxWeekOfMonth). */
export const MAX_WEEK_OF_MONTH = 5

export type ShiftRef = { publicId: string; code: string; name: string }

export type Shift = {
  publicId: string
  code: string
  name: string
  startTime: string
  endTime: string
  /** Derived by the API: true when the end is earlier in the day than the start. */
  crossesMidnight: boolean
  durationMinutes: number
  breakMinutes: number
  graceInMinutes: number
  graceOutMinutes: number
  halfDayThresholdMinutes: number
  fullDayMinutes: number
  punchWindowBeforeMinutes: number
  punchWindowAfterMinutes: number
  isActive: boolean
}

/** `weekOfMonth` 1–5 is that occurrence of the weekday in the month only (days 7n−6 to 7n); null is every week. */
export type WeeklyOffDay = { dayOfWeek: DayOfWeek; weekOfMonth: number | null }

export type WeeklyOffPatternRef = { publicId: string; name: string }

export type WeeklyOffPattern = { publicId: string; name: string; days: WeeklyOffDay[]; isActive: boolean }

export type Holiday = {
  publicId: string
  name: string
  holidayDate: string
  year: number
  /** Listed on the calendar, but doesn't change the day type. */
  isOptional: boolean
  description: string | null
  /** Empty when the holiday applies everywhere. */
  workLocations: OrgUnitRef[]
}

export type CalendarDay = {
  date: string
  dayType: DayType
  /** The effective shift: the rostered one, else the default; null if neither. */
  shift: ShiftRef | null
  /** A roster entry sets the shift or weekly off for this date. */
  isRostered: boolean
  holidayName: string | null
}

export type TeamCalendarRow = {
  employee: EmployeeRef
  department: OrgUnitRef
  joiningDate: string
  defaultShift: ShiftRef | null
  weeklyOffPattern: WeeklyOffPatternRef | null
  days: CalendarDay[]
}

/** An employee's default shift and weekly-off pattern; either may be unset. */
export type EmployeeSchedule = {
  employee: EmployeeRef
  defaultShift: ShiftRef | null
  weeklyOffPattern: WeeklyOffPatternRef | null
}

export type BulkRosterResult = { employeeCount: number; dayCount: number; created: number; updated: number }

export type ClearRosterResult = { employeeCount: number; dayCount: number; removed: number }

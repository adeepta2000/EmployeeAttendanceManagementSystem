import { differenceInCalendarDays, parseISO } from "date-fns"
import { z } from "zod"

import { DaysOfWeek, MAX_WEEK_OF_MONTH } from "@/lib/api/scheduling-types"
import { MINUTES_PER_DAY, formatHours, parseTime, shiftDuration } from "@/lib/shift-time"

import { code, name, optionalId, optionalText, required, requiredId } from "./fields"
import { m } from "./message"

// Scheduling rules, mirroring the API's validators (src/Attendance.Application/Scheduling): ShiftRules,
// the weekly-off pattern's FindProblem, HolidayRules and the roster commands. The API checks them again.

/** ScheduleLimits: the longest range a roster request may cover, and the bounds of every scheduling date. */
export const MAX_RANGE_DAYS = 92
export const EARLIEST_SCHEDULE_DATE = "2000-01-01"
export const LATEST_SCHEDULE_DATE = "2099-12-31"

/** ShiftRules.MaxGraceMinutes and MaxPunchWindowMinutes. */
export const MAX_GRACE_MINUTES = 120
export const MAX_PUNCH_WINDOW_MINUTES = 12 * 60

/** RosterSelection.MaxEmployees: one page of the team roster. */
export const MAX_SELECTED_EMPLOYEES = 100

/** "09:00" or "09:00:00" in, "09:00" out (the API accepts both and returns seconds). */
const time = z
  .string()
  .trim()
  .min(1, required)
  .refine((value) => parseTime(value) !== null, m("validation.time"))
  .transform((value) => value.slice(0, 5))

/** Whole minutes; the form holds text, the BFF gets numbers, and both parse. */
const minutes = (min: number, max?: number) => {
  const message =
    max === undefined ? m("validation.minutesAtLeast", { min }) : m("validation.minutesBetween", { min, max })
  return z
    .union([z.number(), z.string().trim()])
    .refine((value) => value !== "", required)
    .transform(Number)
    .pipe(
      max === undefined
        ? z.number(message).int(message).min(min, message)
        : z.number(message).int(message).min(min, message).max(max, message),
    )
}

export const shiftSchema = z
  .object({
    code: code(50),
    name: name(50),
    startTime: time,
    endTime: time,
    breakMinutes: minutes(0),
    graceInMinutes: minutes(0, MAX_GRACE_MINUTES),
    graceOutMinutes: minutes(0, MAX_GRACE_MINUTES),
    halfDayThresholdMinutes: minutes(1),
    fullDayMinutes: minutes(1),
    punchWindowBeforeMinutes: minutes(0, MAX_PUNCH_WINDOW_MINUTES),
    punchWindowAfterMinutes: minutes(0, MAX_PUNCH_WINDOW_MINUTES),
  })
  .superRefine((shift, ctx) => {
    const issue = (path: keyof typeof shift, message: string) => ctx.addIssue({ code: "custom", path: [path], message })
    // The comparisons need both times; a field with its own error has already been reported.
    const start = parseTime(shift.startTime)
    const end = parseTime(shift.endTime)
    if (start === null || end === null) return
    const duration = shiftDuration(start, end)
    if (duration === 0) return issue("endTime", m("validation.shiftEndsAtStart"))
    if (shift.breakMinutes >= duration) issue("breakMinutes", m("validation.breakTooLong"))
    else if (shift.fullDayMinutes > duration - shift.breakMinutes) {
      issue("fullDayMinutes", m("validation.fullDayTooLong", { hours: formatHours(duration - shift.breakMinutes) }))
    }
    if (shift.halfDayThresholdMinutes > shift.fullDayMinutes) issue("halfDayThresholdMinutes", m("validation.halfDayTooLong"))
    if (shift.punchWindowBeforeMinutes + duration + shift.punchWindowAfterMinutes > MINUTES_PER_DAY) {
      issue("punchWindowAfterMinutes", m("validation.punchWindowTooLong"))
    }
  })

export const weeklyOffDaySchema = z.object({
  dayOfWeek: z.enum(DaysOfWeek),
  weekOfMonth: z.number().int().min(1).max(MAX_WEEK_OF_MONTH).nullable(),
})

/** Days are fixed once the pattern exists; only the name changes afterwards. */
export const weeklyOffPatternSchema = z
  .object({ name: name(100), days: z.array(weeklyOffDaySchema).max(7 * MAX_WEEK_OF_MONTH) })
  .superRefine((pattern, ctx) => {
    const issue = (message: string) => ctx.addIssue({ code: "custom", path: ["days"], message })
    if (pattern.days.length === 0) return issue(m("validation.patternEmpty"))
    const keys = pattern.days.map((day) => `${day.dayOfWeek}:${day.weekOfMonth ?? "*"}`)
    if (new Set(keys).size !== keys.length) return issue(m("validation.patternDuplicate"))
    const everyWeek = new Set(pattern.days.filter((day) => day.weekOfMonth === null).map((day) => day.dayOfWeek))
    if (pattern.days.some((day) => day.weekOfMonth !== null && everyWeek.has(day.dayOfWeek))) {
      return issue(m("validation.patternEveryWeekAndSome"))
    }
    if (everyWeek.size === 7) issue(m("validation.patternEveryDay"))
  })

export const renameWeeklyOffPatternSchema = z.object({ name: name(100) })

const scheduleDate = z
  .string()
  .trim()
  .min(1, required)
  .pipe(z.iso.date(m("validation.date")))
  .refine((value) => value >= EARLIEST_SCHEDULE_DATE && value <= LATEST_SCHEDULE_DATE, m("validation.scheduleDate"))

export const holidaySchema = z.object({
  name: name(100),
  holidayDate: scheduleDate,
  isOptional: z.boolean().default(false),
  description: optionalText(500),
  /** Empty means everywhere. */
  workLocationIds: z
    .array(z.guid())
    .max(50, m("validation.tooManyLocations", { max: 50 }))
    .refine((ids) => new Set(ids).size === ids.length, m("validation.choose"))
    .default([]),
})

/** Null lets the weekly-off pattern decide; true makes the date a weekly off; false a working day. */
const weeklyOffOverride = z
  .boolean()
  .nullish()
  .transform((value) => value ?? null)

/** From ≤ to, at most MAX_RANGE_DAYS days with both included. */
function refineRange(range: { fromDate: string; toDate: string }, ctx: z.RefinementCtx) {
  const days = differenceInCalendarDays(parseISO(range.toDate), parseISO(range.fromDate)) + 1
  if (Number.isNaN(days)) return
  if (days < 1) ctx.addIssue({ code: "custom", path: ["toDate"], message: m("validation.rangeOrder") })
  else if (days > MAX_RANGE_DAYS) {
    ctx.addIssue({ code: "custom", path: ["toDate"], message: m("validation.rangeTooLong", { max: MAX_RANGE_DAYS }) })
  }
}

const employeeSelection = z
  .array(z.guid())
  .min(1, m("validation.selectEmployees"))
  .max(MAX_SELECTED_EMPLOYEES, m("validation.tooManyEmployees", { max: MAX_SELECTED_EMPLOYEES }))
  .refine((ids) => new Set(ids).size === ids.length, m("validation.selectEmployees"))

export const assignRosterSchema = z
  .object({
    employeeIds: employeeSelection,
    fromDate: scheduleDate,
    toDate: scheduleDate,
    shiftId: requiredId,
    isWeeklyOffOverride: weeklyOffOverride,
    remarks: optionalText(300),
  })
  .superRefine(refineRange)

export const clearRosterSchema = z
  .object({ employeeIds: employeeSelection, fromDate: scheduleDate, toDate: scheduleDate })
  .superRefine(refineRange)

export const bulkRosterSchema = z
  .object({
    departmentId: requiredId,
    fromDate: scheduleDate,
    toDate: scheduleDate,
    shiftId: requiredId,
    isWeeklyOffOverride: weeklyOffOverride,
    remarks: optionalText(300),
  })
  .superRefine(refineRange)

/** PUT /api/employees/{id}/schedule. Blank clears either. */
export const employeeScheduleSchema = z.object({ defaultShiftId: optionalId, weeklyOffPatternId: optionalId })

/** Query string of GET /api/employees/{id}/calendar. */
export const employeeCalendarQuerySchema = z
  .object({ fromDate: z.iso.date(), toDate: z.iso.date() })
  .superRefine(refineRange)

/** Query string of GET /api/holidays. */
export const holidayListQuerySchema = z.object({
  year: z.coerce.number().int().min(2000).max(2099),
  workLocationId: z.guid().optional(),
})

/** Query string of GET /api/rosters/calendar. */
export const teamCalendarQuerySchema = z
  .object({
    fromDate: z.iso.date(),
    toDate: z.iso.date(),
    departmentId: z.guid().optional(),
    page: z.coerce.number().int().min(1).max(100_000).optional(),
    pageSize: z.coerce.number().int().min(1).max(MAX_SELECTED_EMPLOYEES).optional(),
  })
  .superRefine(refineRange)

export type TeamCalendarQuery = z.infer<typeof teamCalendarQuerySchema>
export type EmployeeScheduleInput = z.input<typeof employeeScheduleSchema>
export type EmployeeScheduleOutput = z.output<typeof employeeScheduleSchema>
export type ShiftInput = z.input<typeof shiftSchema>
export type ShiftOutput = z.output<typeof shiftSchema>
export type WeeklyOffPatternInput = z.input<typeof weeklyOffPatternSchema>
export type WeeklyOffPatternOutput = z.output<typeof weeklyOffPatternSchema>
export type HolidayInput = z.input<typeof holidaySchema>
export type HolidayOutput = z.output<typeof holidaySchema>
export type AssignRosterInput = z.input<typeof assignRosterSchema>
export type AssignRosterOutput = z.output<typeof assignRosterSchema>
export type ClearRosterOutput = z.output<typeof clearRosterSchema>
export type BulkRosterInput = z.input<typeof bulkRosterSchema>
export type BulkRosterOutput = z.output<typeof bulkRosterSchema>

"use client"

import { addMonths, eachDayOfInterval, endOfMonth, format, getDay, parseISO, startOfMonth } from "date-fns"
import { useFormatter, useTranslations } from "next-intl"

import { DaysOfWeek, MAX_WEEK_OF_MONTH, type DayOfWeek, type WeeklyOffDay } from "@/lib/api/scheduling-types"
import { AttendanceStatuses } from "@/lib/status"
import { cn } from "@/lib/utils"

const WEEKS = Array.from({ length: MAX_WEEK_OF_MONTH }, (_, index) => index + 1)
const off = AttendanceStatuses.weeklyOff

/** Common patterns (FR-2.2's examples first), one click away. */
export const PATTERN_PRESETS: { key: string; days: WeeklyOffDay[] }[] = [
  { key: "friday", days: [{ dayOfWeek: "Friday", weekOfMonth: null }] },
  {
    key: "fridayAlternateSaturdays",
    days: [
      { dayOfWeek: "Friday", weekOfMonth: null },
      { dayOfWeek: "Saturday", weekOfMonth: 2 },
      { dayOfWeek: "Saturday", weekOfMonth: 4 },
    ],
  },
  {
    key: "fridaySaturday",
    days: [
      { dayOfWeek: "Friday", weekOfMonth: null },
      { dayOfWeek: "Saturday", weekOfMonth: null },
    ],
  },
  {
    key: "saturdaySunday",
    days: [
      { dayOfWeek: "Saturday", weekOfMonth: null },
      { dayOfWeek: "Sunday", weekOfMonth: null },
    ],
  },
]

function everyWeek(days: WeeklyOffDay[], day: DayOfWeek) {
  return days.some((d) => d.dayOfWeek === day && d.weekOfMonth === null)
}

function inWeek(days: WeeklyOffDay[], day: DayOfWeek, week: number) {
  return days.some((d) => d.dayOfWeek === day && (d.weekOfMonth === null || d.weekOfMonth === week))
}

/** Days in calendar order, each weekday's particular weeks after it, as the API returns them. */
function sorted(days: WeeklyOffDay[]): WeeklyOffDay[] {
  return [...days].sort(
    (a, b) => DaysOfWeek.indexOf(a.dayOfWeek) - DaysOfWeek.indexOf(b.dayOfWeek) || (a.weekOfMonth ?? 0) - (b.weekOfMonth ?? 0),
  )
}

function toggleEveryWeek(days: WeeklyOffDay[], day: DayOfWeek): WeeklyOffDay[] {
  const others = days.filter((d) => d.dayOfWeek !== day)
  return everyWeek(days, day) ? others : sorted([...others, { dayOfWeek: day, weekOfMonth: null }])
}

/**
 * Toggles one week of one weekday. Turning off a week of an every-week day leaves the other weeks on
 * ("every Saturday but the 3rd"); turning on all five collapses back to every week, which is the same days.
 */
function toggleWeek(days: WeeklyOffDay[], day: DayOfWeek, week: number): WeeklyOffDay[] {
  const others = days.filter((d) => d.dayOfWeek !== day)
  const weeks = new Set(
    everyWeek(days, day) ? WEEKS : days.filter((d) => d.dayOfWeek === day).map((d) => d.weekOfMonth as number),
  )
  if (weeks.has(week)) weeks.delete(week)
  else weeks.add(week)
  const next: WeeklyOffDay[] =
    weeks.size === WEEKS.length
      ? [{ dayOfWeek: day, weekOfMonth: null }]
      : [...weeks].map((w) => ({ dayOfWeek: day, weekOfMonth: w }))
  return sorted([...others, ...next])
}

/**
 * Screen 30's 7-day toggle grid: one column per weekday, an "every week" row, then one row per
 * occurrence of that weekday in the month, which is how alternate Saturdays are set (2nd and 4th).
 * Off days carry the weekly-off letter code as well as its tint, so colour never carries the meaning alone.
 */
export function WeeklyOffGrid({
  value,
  onChange,
  readOnly,
  invalid,
  describedBy,
}: {
  value: WeeklyOffDay[]
  onChange?: (days: WeeklyOffDay[]) => void
  readOnly?: boolean
  invalid?: boolean
  describedBy?: string
}) {
  const t = useTranslations("scheduling.patterns.grid")
  const tDays = useTranslations("scheduling.days")
  const edit = !readOnly && onChange

  const cell = (key: string, on: boolean, inherited: boolean, label: string, toggle: () => void) => (
    <td key={key} className="p-0.25">
      {edit ? (
        <button
          type="button"
          aria-pressed={on}
          aria-label={label}
          onClick={toggle}
          className={cn(
            "flex h-4 w-full items-center justify-center rounded-sm border text-12 font-semibold transition-colors",
            on
              ? cn(off.tint, off.border, off.text, inherited && "opacity-60")
              : "border-rule bg-surface text-ink-muted hover:border-brand hover:bg-brand-soft",
          )}
        >
          {on ? off.code : <span aria-hidden="true">·</span>}
        </button>
      ) : (
        <span
          aria-label={label}
          className={cn(
            "flex h-3.5 w-full items-center justify-center rounded-sm border text-12 font-semibold",
            on ? cn(off.tint, off.border, off.text, inherited && "opacity-60") : "border-rule bg-surface text-ink-muted",
          )}
        >
          {on ? off.code : <span aria-hidden="true">·</span>}
        </span>
      )}
    </td>
  )

  return (
    <table
      aria-describedby={describedBy}
      aria-invalid={invalid || undefined}
      className={cn("w-full table-fixed border-separate border-spacing-0", invalid && "rounded-md ring-2 ring-danger/40")}
    >
      <caption className="sr-only">{t("caption")}</caption>
      <thead>
        <tr>
          <th scope="col" className="w-[120px]">
            <span className="sr-only">{t("week")}</span>
          </th>
          {DaysOfWeek.map((day) => (
            <th key={day} scope="col" className="pb-0.5 text-center text-12 font-semibold text-ink-muted">
              <abbr title={tDays(`long.${day}`)} className="no-underline">
                {tDays(`short.${day}`)}
              </abbr>
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        <tr>
          <th scope="row" className="pr-1 text-left text-13 font-semibold">
            {t("everyWeek")}
          </th>
          {DaysOfWeek.map((day) =>
            cell(day, everyWeek(value, day), false, t("everyWeekLabel", { day: tDays(`long.${day}`) }), () =>
              onChange?.(toggleEveryWeek(value, day)),
            ),
          )}
        </tr>
        {WEEKS.map((week) => (
          <tr key={week}>
            <th scope="row" className="pr-1 text-left text-13 font-normal text-ink-muted">
              {t("weekRow", { week })}{" "}
              <span className="font-mono text-12">{t("weekDays", { from: 7 * week - 6, to: Math.min(7 * week, 31) })}</span>
            </th>
            {DaysOfWeek.map((day) =>
              cell(day, inWeek(value, day, week), everyWeek(value, day), t("weekLabel", { day: tDays(`long.${day}`), week }), () =>
                onChange?.(toggleWeek(value, day, week)),
              ),
            )}
          </tr>
        ))}
      </tbody>
    </table>
  )
}

/** "Off every Friday and the 2nd and 4th Saturday." */
export function usePatternSummary() {
  const t = useTranslations("scheduling.patterns")
  const tDays = useTranslations("scheduling.days")
  const formatter = useFormatter()
  return (days: WeeklyOffDay[]) => {
    if (days.length === 0) return t("summary.none")
    const parts = DaysOfWeek.flatMap((day) => {
      if (everyWeek(days, day)) return [t("summary.every", { day: tDays(`long.${day}`) })]
      const weeks = days.filter((d) => d.dayOfWeek === day).map((d) => d.weekOfMonth as number)
      if (weeks.length === 0) return []
      const ordinals = formatter.list(weeks.map((week) => t("summary.ordinal", { week })))
      return [t("summary.some", { weeks: ordinals, day: tDays(`long.${day}`), count: weeks.length })]
    })
    return t("summary.sentence", { list: formatter.list(parts) })
  }
}

/**
 * Whether a date is a weekly off under these days: the weekday matches and, for a particular week, it is
 * that occurrence (days 7n−6 to 7n). Mirrors WeeklyOffPattern.IsWeeklyOff, for the preview only: the API
 * decides every real date (the calendar and the team roster show its answer).
 */
export function isWeeklyOff(days: WeeklyOffDay[], date: Date): boolean {
  const day = DaysOfWeek[getDay(date)]
  const week = Math.floor((date.getDate() - 1) / 7) + 1
  return days.some((d) => d.dayOfWeek === day && (d.weekOfMonth === null || d.weekOfMonth === week))
}

/** A few months as small calendars, with the pattern's weekly offs marked: what the numbers mean in practice. */
export function PatternPreview({ days, from, months = 3 }: { days: WeeklyOffDay[]; from: string; months?: number }) {
  const t = useTranslations("scheduling.patterns.preview")
  const tDays = useTranslations("scheduling.days")
  const first = startOfMonth(parseISO(from))

  return (
    <div className="grid grid-cols-[repeat(auto-fill,minmax(216px,1fr))] gap-2">
      {Array.from({ length: months }, (_, index) => {
        const month = addMonths(first, index)
        const dates = eachDayOfInterval({ start: month, end: endOfMonth(month) })
        const offs = dates.filter((date) => isWeeklyOff(days, date))
        return (
          <figure key={index} className="rounded-md border border-rule bg-surface p-1">
            <figcaption className="mb-0.5 flex items-baseline justify-between text-13 font-semibold">
              {format(month, "MMMM yyyy")}
              <span className="text-12 font-normal text-ink-muted">{t("count", { count: offs.length })}</span>
            </figcaption>
            <div className="grid grid-cols-7 gap-0.25 text-center text-12" role="presentation">
              {DaysOfWeek.map((day) => (
                <span key={day} aria-hidden="true" className="text-ink-muted">
                  {tDays(`initial.${day}`)}
                </span>
              ))}
              {Array.from({ length: getDay(month) }, (_, blank) => (
                <span key={`blank-${blank}`} aria-hidden="true" />
              ))}
              {dates.map((date) => {
                const isOff = isWeeklyOff(days, date)
                return (
                  <span
                    key={date.getDate()}
                    aria-hidden="true"
                    className={cn(
                      "flex h-2.5 items-center justify-center rounded-sm font-mono",
                      isOff ? cn(off.tint, off.text, "font-semibold") : "text-ink",
                    )}
                  >
                    {date.getDate()}
                  </span>
                )
              })}
            </div>
            <p className="sr-only">
              {offs.length > 0 ? t("offDays", { dates: offs.map((date) => format(date, "EEEE d")).join(", ") }) : t("noOffDays")}
            </p>
          </figure>
        )
      })}
    </div>
  )
}

/**
 * A one-line strip for the list: each weekday, solid if it is off every week, dashed if only in some
 * weeks. The summary text beside it says which.
 */
export function PatternStrip({ days }: { days: WeeklyOffDay[] }) {
  const tDays = useTranslations("scheduling.days")
  return (
    <span aria-hidden="true" className="flex gap-0.25">
      {DaysOfWeek.map((day) => {
        const every = everyWeek(days, day)
        const some = !every && days.some((d) => d.dayOfWeek === day)
        return (
          <span
            key={day}
            className={cn(
              "flex size-2.5 items-center justify-center rounded-sm border text-12",
              every || some ? cn(off.tint, off.border, off.text, "font-semibold", some && "border-dashed") : "border-rule text-ink-muted",
            )}
          >
            {tDays(`initial.${day}`)}
          </span>
        )
      })}
    </span>
  )
}

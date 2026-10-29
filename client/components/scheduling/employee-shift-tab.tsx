"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { addMonths, eachDayOfInterval, endOfMonth, format, getDay, parseISO, startOfMonth } from "date-fns"
import { CalendarRangeIcon, ChevronLeftIcon, ChevronRightIcon, MoonIcon, PencilIcon } from "lucide-react"
import Link from "next/link"
import { useTranslations } from "next-intl"
import { useState, type ReactNode } from "react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"

import { Section } from "@/components/employees/employee-frame"
import { applyApiErrors } from "@/components/forms/api-errors"
import { SelectField, type Option } from "@/components/forms/fields"
import { FormAlert } from "@/components/forms/form-alert"
import { useSession } from "@/components/shell/session-context"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { Spinner } from "@/components/ui/spinner"
import { useErrorText } from "@/lib/api/errors"
import {
  useEmployeeCalendar,
  useEmployeeSchedule,
  useSetEmployeeSchedule,
  useShifts,
  useWeeklyOffPatterns,
} from "@/lib/api/scheduling-queries"
import { DaysOfWeek, type CalendarDay, type Shift } from "@/lib/api/scheduling-types"
import type { Employee } from "@/lib/api/types"
import { todayInBusinessZone } from "@/lib/dates"
import { Permissions } from "@/lib/permissions"
import { employeeScheduleSchema, type EmployeeScheduleInput, type EmployeeScheduleOutput } from "@/lib/schemas/scheduling"
import { formatHours, formatTime, parseTime } from "@/lib/shift-time"
import { AttendanceStatuses } from "@/lib/status"
import { cn } from "@/lib/utils"

import { ShiftDayBar } from "./shift-timeline"
import { PatternStrip, usePatternSummary } from "./weekly-off-grid"

/**
 * Screen 27's Shift tab (FR-2.2, FR-2.3): the employee's default shift and weekly-off pattern, which
 * roster.write can change, and a month of how each date resolves once rosters and holidays apply.
 */
export function EmployeeShiftTab({ employee }: { employee: Employee }) {
  return (
    <>
      <ScheduleSection employee={employee} />
      <MonthSection employee={employee} />
    </>
  )
}

function ScheduleSection({ employee }: { employee: Employee }) {
  const t = useTranslations("scheduling.employee")
  const errorText = useErrorText()
  const { can } = useSession()
  const canWrite = can(Permissions.Roster.Write)
  const schedule = useEmployeeSchedule(employee.publicId)
  const shifts = useShifts()
  const patterns = useWeeklyOffPatterns()
  const summary = usePatternSummary()
  const [editing, setEditing] = useState(false)

  if (schedule.isPending) {
    return (
      <Section title={t("title")}>
        <div aria-hidden="true" className="flex flex-col gap-1.5">
          <Skeleton className="h-2 w-30" />
          <Skeleton className="h-2 w-24" />
        </div>
      </Section>
    )
  }
  if (schedule.isError) {
    return (
      <Section title={t("title")}>
        <FormAlert>{errorText(schedule.error)}</FormAlert>
      </Section>
    )
  }

  const { defaultShift, weeklyOffPattern } = schedule.data
  const shift = shifts.data?.find((s) => s.publicId === defaultShift?.publicId)
  const pattern = patterns.data?.find((p) => p.publicId === weeklyOffPattern?.publicId)

  if (editing) {
    return (
      <Section title={t("title")}>
        <ScheduleForm
          employee={employee}
          current={{ shiftId: defaultShift?.publicId, patternId: weeklyOffPattern?.publicId }}
          shifts={shifts.data ?? []}
          onDone={() => setEditing(false)}
        />
      </Section>
    )
  }

  return (
    <Section title={t("title")}>
      <dl className="grid grid-cols-[minmax(0,180px)_1fr] gap-x-3 gap-y-1.5 text-14">
        <dt className="text-ink-muted">{t("defaultShift")}</dt>
        <dd className="min-w-0">
          {defaultShift ? (
            <span className="flex flex-wrap items-center gap-1">
              <span>
                <span className="font-mono">{defaultShift.code}</span> · {defaultShift.name}
              </span>
              {shift && <ShiftHours shift={shift} />}
              {shift && !shift.isActive && <span className="text-13 text-ink-muted">{t("retired")}</span>}
            </span>
          ) : (
            <span className="text-ink-muted">{t("noShift")}</span>
          )}
        </dd>
        <dt className="text-ink-muted">{t("weeklyOffPattern")}</dt>
        <dd className="min-w-0">
          {weeklyOffPattern ? (
            <span className="flex flex-col gap-0.5">
              <span className="flex flex-wrap items-center gap-1">
                {weeklyOffPattern.name}
                {pattern && !pattern.isActive && <span className="text-13 text-ink-muted">{t("retired")}</span>}
              </span>
              {pattern && (
                <span className="flex flex-wrap items-center gap-1 text-13 text-ink-muted">
                  <PatternStrip days={pattern.days} />
                  {summary(pattern.days)}
                </span>
              )}
            </span>
          ) : (
            <span className="text-ink-muted">{t("noPattern")}</span>
          )}
        </dd>
      </dl>
      <p className="mt-2 text-13 text-ink-muted">{t("appliesFromNow")}</p>
      {canWrite && (
        <div className="mt-2 flex flex-wrap gap-1">
          <Button variant="outline" onClick={() => setEditing(true)}>
            <PencilIcon aria-hidden="true" />
            {t("change")}
          </Button>
          {employee.isActive && (
            <Button asChild variant="ghost">
              <Link href={`/setup/rosters?department=${employee.department.publicId}`}>
                <CalendarRangeIcon aria-hidden="true" />
                {t("openRoster")}
              </Link>
            </Button>
          )}
        </div>
      )}
    </Section>
  )
}

function ShiftHours({ shift }: { shift: Shift }) {
  const t = useTranslations("scheduling.employee")
  const start = parseTime(shift.startTime) ?? 0
  const end = parseTime(shift.endTime) ?? 0
  return (
    <span className="flex items-center gap-1 text-13 text-ink-muted">
      <span className="font-mono">
        {formatTime(start)}–{formatTime(end)}
        {shift.crossesMidnight && <sup aria-hidden="true"> +1</sup>}
      </span>
      <ShiftDayBar start={start} end={end} className="w-12" />
      <span className="font-mono">{formatHours(shift.durationMinutes)}</span>
      {shift.crossesMidnight && (
        <>
          <MoonIcon aria-hidden="true" className="size-1.5" />
          <span className="sr-only">{t("nightShift")}</span>
        </>
      )}
    </span>
  )
}

/**
 * The two choices as selects. Active shifts and patterns, plus the current one even if it has retired
 * since: the API keeps an unchanged assignment, and the form must be able to show it.
 */
function ScheduleForm({
  employee,
  current,
  shifts,
  onDone,
}: {
  employee: Employee
  current: { shiftId?: string; patternId?: string }
  shifts: Shift[]
  onDone: () => void
}) {
  const t = useTranslations("scheduling.employee")
  const tCommon = useTranslations("common")
  const errorText = useErrorText()
  const patterns = useWeeklyOffPatterns()
  const save = useSetEmployeeSchedule(employee.publicId)
  const [formError, setFormError] = useState<string | null>(null)

  const form = useForm<EmployeeScheduleInput, unknown, EmployeeScheduleOutput>({
    resolver: zodResolver(employeeScheduleSchema),
    defaultValues: { defaultShiftId: current.shiftId ?? "", weeklyOffPatternId: current.patternId ?? "" },
  })

  const shiftOptions: Option[] = shifts
    .filter((s) => s.isActive || s.publicId === current.shiftId)
    .map((s) => ({
      value: s.publicId,
      label: `${s.code} · ${s.name} (${formatTime(parseTime(s.startTime) ?? 0)}–${formatTime(parseTime(s.endTime) ?? 0)})${s.isActive ? "" : ` · ${t("retired")}`}`,
    }))
  const patternOptions: Option[] = (patterns.data ?? [])
    .filter((p) => p.isActive || p.publicId === current.patternId)
    .map((p) => ({ value: p.publicId, label: p.isActive ? p.name : `${p.name} · ${t("retired")}` }))

  // No <form> element: this sits inside the employee page, whose edit mode is itself a form.
  const submit = form.handleSubmit(async (values) => {
    setFormError(null)
    try {
      await save.mutateAsync(values)
      toast.success(t("saved", { name: employee.fullName }))
      onDone()
    } catch (error) {
      const applied = applyApiErrors(error, form.setError, ["defaultShiftId", "weeklyOffPatternId"], {
        "employee.shift_not_found": "defaultShiftId",
        "employee.weekly_off_pattern_not_found": "weeklyOffPatternId",
      })
      if (!applied) setFormError(errorText(error))
    }
  })

  return (
    <div className="flex flex-col gap-2">
      <FormAlert>{formError}</FormAlert>
      <div className="grid grid-cols-2 gap-2">
        <SelectField
          control={form.control}
          name="defaultShiftId"
          label={t("defaultShift")}
          options={shiftOptions}
          noneLabel={t("noShiftOption")}
          description={t("shiftHint")}
        />
        <SelectField
          control={form.control}
          name="weeklyOffPatternId"
          label={t("weeklyOffPattern")}
          options={patternOptions}
          noneLabel={t("noPatternOption")}
          description={t("patternHint")}
        />
      </div>
      <p className="text-13 text-ink-muted">{t("appliesFromNow")}</p>
      <div className="flex justify-end gap-1">
        <Button type="button" variant="outline" onClick={onDone} disabled={form.formState.isSubmitting}>
          {tCommon("cancel")}
        </Button>
        <Button type="button" onClick={() => void submit()} disabled={form.formState.isSubmitting}>
          {form.formState.isSubmitting && <Spinner />}
          {tCommon("save")}
        </Button>
      </div>
    </div>
  )
}

/** One month of the API's resolved calendar: weekly offs and holidays by letter code, otherwise the shift. */
function MonthSection({ employee }: { employee: Employee }) {
  const t = useTranslations("scheduling.employee")
  const tDays = useTranslations("scheduling.days")
  const errorText = useErrorText()
  const today = todayInBusinessZone()
  const [month, setMonth] = useState(() => startOfMonth(parseISO(today)))
  const fromDate = format(month, "yyyy-MM-dd")
  const toDate = format(endOfMonth(month), "yyyy-MM-dd")
  const calendar = useEmployeeCalendar(employee.publicId, fromDate, toDate)
  const byDate = new Map((calendar.data ?? []).map((day) => [day.date, day]))
  // Weeks as rows, Sunday first, with blanks before the 1st and after the last day.
  const slots: (Date | null)[] = [
    ...Array.from({ length: getDay(month) }, () => null),
    ...eachDayOfInterval({ start: month, end: endOfMonth(month) }),
  ]
  const weeks = Array.from({ length: Math.ceil(slots.length / 7) }, (_, w) =>
    Array.from({ length: 7 }, (_, d) => slots[w * 7 + d] ?? null),
  )

  return (
    <Section title={t("monthTitle")}>
      <div className="mb-1.5 flex items-center gap-0.5">
        <Button variant="outline" size="icon-sm" onClick={() => setMonth(addMonths(month, -1))} aria-label={t("previousMonth")}>
          <ChevronLeftIcon aria-hidden="true" />
        </Button>
        <h3 className="min-w-16 text-center text-14 font-semibold" aria-live="polite">
          {format(month, "MMMM yyyy")}
        </h3>
        <Button variant="outline" size="icon-sm" onClick={() => setMonth(addMonths(month, 1))} aria-label={t("nextMonth")}>
          <ChevronRightIcon aria-hidden="true" />
        </Button>
      </div>

      {calendar.isError ? (
        <FormAlert>{errorText(calendar.error)}</FormAlert>
      ) : (
        <table
          aria-busy={calendar.isPending || calendar.isPlaceholderData || undefined}
          className={cn("w-full table-fixed border-separate border-spacing-0.5", calendar.isPlaceholderData && "opacity-60")}
        >
          <caption className="sr-only">{t("monthLabel", { name: employee.fullName, month: format(month, "MMMM yyyy") })}</caption>
          <thead>
            <tr>
              {DaysOfWeek.map((day) => (
                <th key={day} scope="col" className="text-12 font-normal text-ink-muted">
                  <abbr title={tDays(`long.${day}`)} className="no-underline">
                    {tDays(`short.${day}`)}
                  </abbr>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {weeks.map((week, index) => (
              <tr key={index}>
                {week.map((date, slot) => {
                  if (!date) return <td key={`blank-${slot}`} />
                  const value = format(date, "yyyy-MM-dd")
                  return (
                    <DayCell
                      key={value}
                      date={date}
                      day={byDate.get(value)}
                      loading={calendar.isPending}
                      beforeJoining={value < employee.joiningDate}
                      isToday={value === today}
                    />
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <ul className="mt-1.5 flex flex-wrap gap-x-2 gap-y-0.5 text-12 text-ink-muted" aria-label={t("legend.label")}>
        <LegendItem swatch={<span className="h-2 w-3 rounded-sm border border-brand/40 bg-brand-soft" />}>{t("legend.rostered")}</LegendItem>
        <LegendItem swatch={<span className="h-2 w-3 rounded-sm border border-rule" />}>{t("legend.default")}</LegendItem>
        <LegendItem swatch={<Code status="weeklyOff" />}>{t("legend.weeklyOff")}</LegendItem>
        <LegendItem swatch={<Code status="holiday" />}>{t("legend.holiday")}</LegendItem>
      </ul>
    </Section>
  )
}

function DayCell({
  date,
  day,
  loading,
  beforeJoining,
  isToday,
}: {
  date: Date
  day: CalendarDay | undefined
  loading: boolean
  beforeJoining: boolean
  isToday: boolean
}) {
  const t = useTranslations("scheduling.roster.cell")
  const status = day?.dayType === "WeeklyOff" ? "weeklyOff" : day?.dayType === "Holiday" ? "holiday" : null
  const what = !day
    ? ""
    : beforeJoining
      ? t("beforeJoining")
      : [
          status === "weeklyOff" ? t("weeklyOff") : status === "holiday" ? t("holiday", { name: day.holidayName ?? "" }) : null,
          day.shift ? t("shift", { code: day.shift.code, name: day.shift.name }) : t("noShift"),
          day.isRostered ? t("rostered") : t("default"),
        ]
          .filter(Boolean)
          .join(", ")

  return (
    <td
      aria-label={`${format(date, "EEEE d MMMM")}${what ? `: ${what}` : ""}`}
      title={day?.holidayName ?? day?.shift?.name ?? undefined}
      className={cn(
        "h-6 rounded-sm border p-0.5 align-top text-12",
        day?.isRostered && !status ? "border-brand/40 bg-brand-soft" : "border-rule bg-surface",
        beforeJoining && "bg-canvas text-ink-muted",
        isToday && "ring-2 ring-brand ring-inset",
      )}
    >
      <span className="flex h-full flex-col justify-between">
      <span aria-hidden="true" className="font-mono text-ink-muted">
        {format(date, "d")}
      </span>
      <span aria-hidden="true" className="self-end">
        {loading ? (
          <Skeleton className="h-1.5 w-3" />
        ) : !day || beforeJoining ? null : status ? (
          <Code status={status} rostered={day.isRostered} />
        ) : day.shift ? (
          <span className={cn("font-mono", day.isRostered ? "font-semibold text-brand" : "text-ink")}>{day.shift.code}</span>
        ) : (
          <span className="text-ink-muted">—</span>
        )}
      </span>
      </span>
    </td>
  )
}

/** A weekly-off or holiday letter code on its status tint (§4.2); a dot when a roster entry set it. */
function Code({ status, rostered }: { status: "weeklyOff" | "holiday"; rostered?: boolean }) {
  const style = AttendanceStatuses[status]
  return (
    <span className={cn("relative inline-flex h-2 items-center rounded-pill border px-0.5 font-semibold", style.tint, style.border, style.text)}>
      {style.code}
      {rostered && <span className="absolute -top-0.25 -right-0.25 size-0.5 rounded-full bg-brand" />}
    </span>
  )
}

function LegendItem({ swatch, children }: { swatch: ReactNode; children: ReactNode }) {
  return (
    <li className="flex items-center gap-0.5">
      <span aria-hidden="true" className="flex items-center">
        {swatch}
      </span>
      {children}
    </li>
  )
}

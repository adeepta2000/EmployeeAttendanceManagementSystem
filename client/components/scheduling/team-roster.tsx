"use client"

import { addDays, addMonths, eachDayOfInterval, endOfMonth, format, isValid, parseISO, startOfMonth } from "date-fns"
import { CalendarRangeIcon, ChevronLeftIcon, ChevronRightIcon, MoonIcon, XIcon } from "lucide-react"
import { useTranslations } from "next-intl"
import { memo, useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent, type PointerEvent } from "react"
import { toast } from "sonner"
import { z } from "zod"

import { Pagination } from "@/components/data-table/pagination"
import { DensityToggle } from "@/components/data-table/density-toggle"
import { FormAlert } from "@/components/forms/form-alert"
import { PageHeader } from "@/components/page-header"
import { useDensity } from "@/components/shell/density"
import { useSession } from "@/components/shell/session-context"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import { Spinner } from "@/components/ui/spinner"
import { useErrorText } from "@/lib/api/errors"
import { useDepartments } from "@/lib/api/queries"
import { useRosterMutations, useShifts, useTeamCalendar } from "@/lib/api/scheduling-queries"
import type { CalendarDay, Shift, TeamCalendarRow } from "@/lib/api/scheduling-types"
import { todayInBusinessZone } from "@/lib/dates"
import { Permissions } from "@/lib/permissions"
import { AttendanceStatuses } from "@/lib/status"
import { useUrlState } from "@/lib/use-url-state"
import { cn } from "@/lib/utils"

import { BulkRosterDialog, OVERRIDES, shiftOptions, toOverride, type OverrideChoice } from "./roster-dialogs"

const PAGE_SIZES = [25, 50, 100] as const

/** The view in the URL (§4.5): department, period and page, so "my line, this month" can be shared. */
const paramsSchema = z.object({
  department: z.guid().optional().catch(undefined),
  view: z.enum(["month", "fortnight"]).catch("month").default("month"),
  start: z.iso.date().optional().catch(undefined),
  page: z.coerce.number().int().min(1).max(100_000).catch(1).default(1),
  pageSize: z.coerce
    .number()
    .refine((size) => (PAGE_SIZES as readonly number[]).includes(size))
    .catch(50)
    .default(50),
})

type Point = { r: number; c: number }
type Rect = { r1: number; r2: number; c1: number; c2: number }

function toRect(a: Point, b: Point): Rect {
  return { r1: Math.min(a.r, b.r), r2: Math.max(a.r, b.r), c1: Math.min(a.c, b.c), c2: Math.max(a.c, b.c) }
}

const ymd = (date: Date) => format(date, "yyyy-MM-dd")

/** The period on screen: a calendar month, or 14 days from a chosen date. */
function periodOf(view: "month" | "fortnight", start: string | undefined) {
  const today = parseISO(todayInBusinessZone())
  const anchor = start && isValid(parseISO(start)) ? parseISO(start) : today
  const from = view === "month" ? startOfMonth(anchor) : anchor
  const to = view === "month" ? endOfMonth(anchor) : addDays(anchor, 13)
  return { from, to, dates: eachDayOfInterval({ start: from, end: to }) }
}

/**
 * Screen 16: employees down the side, dates across, each cell the shift the API resolved for that day.
 * Drag across cells (or Shift+arrow keys) to select a block, then assign a shift to all of it in one
 * request, or reset it to each employee's default. A manager sees their reporting tree; HR the company,
 * usually one department at a time.
 */
export function TeamRoster() {
  const t = useTranslations("scheduling.roster")
  const errorText = useErrorText()
  const { can } = useSession()
  const canWrite = can(Permissions.Roster.Write)
  const canChooseDepartment = can(Permissions.Scope.Company)
  const { density } = useDensity()
  const [params, setParams] = useUrlState(paramsSchema)
  const period = useMemo(() => periodOf(params.view, params.start), [params.view, params.start])
  const query = {
    fromDate: ymd(period.from),
    toDate: ymd(period.to),
    departmentId: params.department,
    page: params.page,
    pageSize: params.pageSize,
  }
  const calendar = useTeamCalendar(query)
  const shifts = useShifts()
  const departments = useDepartments()
  const [bulkOpen, setBulkOpen] = useState(false)

  const rows = useMemo(() => calendar.data?.items ?? [], [calendar.data])
  const dates = period.dates
  const today = todayInBusinessZone()

  // Selection: an anchor and the cursor, the corners of the selected block. The cursor is also the
  // grid's one tabbable cell (roving tabindex).
  const [anchor, setAnchor] = useState<Point | null>(null)
  const [cursor, setCursor] = useState<Point>({ r: 0, c: 0 })
  const dragging = useRef(false)
  const gridRef = useRef<HTMLTableElement>(null)
  const selection = anchor ? toRect(anchor, cursor) : null

  // A new period, department or page is a different grid: what was selected no longer means anything.
  const gridKey = `${query.fromDate}|${query.departmentId ?? ""}|${query.page}|${query.pageSize}`
  const [lastKey, setLastKey] = useState(gridKey)
  if (lastKey !== gridKey) {
    setLastKey(gridKey)
    setAnchor(null)
    setCursor({ r: 0, c: 0 })
  }

  useEffect(() => {
    const stop = () => {
      dragging.current = false
    }
    window.addEventListener("pointerup", stop)
    window.addEventListener("pointercancel", stop)
    return () => {
      window.removeEventListener("pointerup", stop)
      window.removeEventListener("pointercancel", stop)
    }
  }, [])

  const focusCell = useCallback((point: Point) => {
    gridRef.current?.querySelector<HTMLElement>(`[data-cell="${point.r}:${point.c}"]`)?.focus()
  }, [])

  const onCellPointerDown = useCallback(
    (point: Point, event: PointerEvent) => {
      if (!canWrite || event.button !== 0) return
      event.preventDefault()
      dragging.current = true
      if (!event.shiftKey) setAnchor(point)
      else setAnchor((current) => current ?? point)
      setCursor(point)
      focusCell(point)
    },
    [canWrite, focusCell],
  )

  const onCellPointerEnter = useCallback((point: Point) => {
    if (dragging.current) setCursor(point)
  }, [])

  const onGridKeyDown = (event: KeyboardEvent) => {
    if (rows.length === 0) return
    const moves: Record<string, Point> = {
      ArrowUp: { r: cursor.r - 1, c: cursor.c },
      ArrowDown: { r: cursor.r + 1, c: cursor.c },
      ArrowLeft: { r: cursor.r, c: cursor.c - 1 },
      ArrowRight: { r: cursor.r, c: cursor.c + 1 },
      Home: { r: cursor.r, c: 0 },
      End: { r: cursor.r, c: dates.length - 1 },
    }
    if (event.key === "Escape") {
      setAnchor(null)
      return
    }
    if ((event.key === " " || event.key === "Enter") && canWrite) {
      event.preventDefault()
      setAnchor(cursor)
      return
    }
    const next = moves[event.key]
    if (!next) return
    event.preventDefault()
    const point = { r: clamp(next.r, 0, rows.length - 1), c: clamp(next.c, 0, dates.length - 1) }
    if (canWrite) {
      if (event.shiftKey) setAnchor((current) => current ?? cursor)
      else setAnchor(point)
    }
    setCursor(point)
    focusCell(point)
  }

  const selectColumn = (c: number) => {
    if (!canWrite || rows.length === 0) return
    setAnchor({ r: 0, c })
    setCursor({ r: rows.length - 1, c })
  }
  const selectRow = (r: number) => {
    if (!canWrite) return
    setAnchor({ r, c: 0 })
    setCursor({ r, c: dates.length - 1 })
  }

  const shiftsById = useMemo(() => new Map((shifts.data ?? []).map((shift) => [shift.publicId, shift])), [shifts.data])

  const step = (direction: 1 | -1) => {
    const next = params.view === "month" ? addMonths(period.from, direction) : addDays(period.from, 14 * direction)
    setParams({ start: ymd(next), page: 1 })
  }

  const departmentName = departments.data?.find((d) => d.publicId === params.department)?.name
  const periodLabel =
    params.view === "month"
      ? format(period.from, "MMMM yyyy")
      : `${format(period.from, "d MMM")} – ${format(period.to, "d MMM yyyy")}`

  return (
    <div className="flex h-full flex-col">
      <PageHeader
        title={t("title")}
        description={canChooseDepartment ? t("descriptionCompany") : t("descriptionTeam")}
        actions={
          canWrite && (
            <Button variant="outline" onClick={() => setBulkOpen(true)}>
              <CalendarRangeIcon aria-hidden="true" />
              {t("bulk.open")}
            </Button>
          )
        }
      />

      <div role="search" aria-label={t("filters")} className="flex flex-wrap items-center gap-1 border-b border-rule bg-surface px-3 py-1">
        <Select
          value={params.department ?? "all"}
          onValueChange={(value) => setParams({ department: value === "all" ? undefined : value, page: 1 })}
        >
          <SelectTrigger aria-label={t("department")} className="min-w-20">
            <SelectValue>{departmentName ?? t(canChooseDepartment ? "allDepartments" : "myTeam")}</SelectValue>
          </SelectTrigger>
          <SelectContent position="popper">
            <SelectItem value="all">{t(canChooseDepartment ? "allDepartments" : "myTeam")}</SelectItem>
            {(departments.data ?? [])
              .filter((d) => d.isActive)
              .map((d) => (
                <SelectItem key={d.publicId} value={d.publicId}>
                  {d.name}
                </SelectItem>
              ))}
          </SelectContent>
        </Select>

        <div className="flex items-center gap-0.5">
          <Button variant="outline" size="icon-sm" onClick={() => step(-1)} aria-label={t(`previous.${params.view}`)}>
            <ChevronLeftIcon aria-hidden="true" />
          </Button>
          <h2 className="min-w-22 text-center text-14 font-semibold tabular" aria-live="polite">
            {periodLabel}
          </h2>
          <Button variant="outline" size="icon-sm" onClick={() => step(1)} aria-label={t(`next.${params.view}`)}>
            <ChevronRightIcon aria-hidden="true" />
          </Button>
          <Button variant="ghost" size="sm" onClick={() => setParams({ start: undefined, page: 1 })}>
            {t("today")}
          </Button>
        </div>

        <div role="radiogroup" aria-label={t("viewLabel")} className="inline-flex rounded-md border border-rule p-0.25">
          {(["month", "fortnight"] as const).map((view) => (
            <button
              key={view}
              type="button"
              role="radio"
              aria-checked={params.view === view}
              onClick={() => setParams({ view, start: ymd(period.from), page: 1 })}
              className={cn(
                "h-3 rounded-sm px-1 text-13 font-medium",
                params.view === view ? "bg-brand text-primary-foreground" : "text-ink-muted hover:text-ink",
              )}
            >
              {t(`views.${view}`)}
            </button>
          ))}
        </div>

        <div className="ml-auto">
          <DensityToggle />
        </div>
      </div>

      {calendar.isError ? (
        <div className="p-3">
          <FormAlert>{errorText(calendar.error)}</FormAlert>
        </div>
      ) : (
        <div className="relative min-h-0 flex-1 overflow-auto bg-surface">
          <table
            ref={gridRef}
            role="grid"
            aria-label={t("gridLabel", { period: periodLabel })}
            aria-multiselectable={canWrite || undefined}
            aria-busy={calendar.isPending || calendar.isPlaceholderData || undefined}
            onKeyDown={onGridKeyDown}
            className={cn(
              "border-separate border-spacing-0 select-none",
              calendar.isPlaceholderData && "opacity-60 transition-opacity",
            )}
          >
            <thead className="sticky top-0 z-20 bg-surface">
              <tr>
                <th
                  scope="col"
                  className="sticky left-0 z-30 w-[232px] min-w-[232px] border-r border-b border-rule bg-surface px-1.5 text-left text-12 font-semibold text-ink-muted"
                >
                  {t("employee")}
                </th>
                {dates.map((date, c) => {
                  const value = ymd(date)
                  const inSelection = selection && c >= selection.c1 && c <= selection.c2
                  return (
                    <th
                      key={value}
                      scope="col"
                      className={cn(
                        "border-b border-rule p-0 text-center text-12 font-normal",
                        params.view === "month" ? "w-5 min-w-5" : "w-9 min-w-9",
                        value === today && "bg-brand-soft",
                      )}
                    >
                      <button
                        type="button"
                        tabIndex={-1}
                        onClick={() => selectColumn(c)}
                        disabled={!canWrite}
                        aria-label={t("selectDate", { date: format(date, "EEEE d MMMM") })}
                        className={cn(
                          "flex h-5 w-full flex-col items-center justify-center leading-tight enabled:hover:bg-canvas",
                          inSelection && "text-brand",
                        )}
                      >
                        <span className={cn("font-mono text-13 font-semibold", value === today ? "text-brand" : "text-ink")}>
                          {format(date, "d")}
                        </span>
                        <span className="text-ink-muted">{format(date, params.view === "month" ? "EEEEE" : "EEE")}</span>
                      </button>
                    </th>
                  )
                })}
              </tr>
            </thead>
            <tbody>
              {calendar.isPending ? (
                <SkeletonRows count={Math.min(params.pageSize, 10)} columns={dates.length} density={density} wide={params.view !== "month"} />
              ) : rows.length === 0 ? (
                <tr>
                  <td colSpan={dates.length + 1} className="sticky left-0 px-3 py-6 text-14 text-ink-muted">
                    {params.department ? t("emptyDepartment") : canChooseDepartment ? t("emptyCompany") : t("emptyTeam")}
                  </td>
                </tr>
              ) : (
                rows.map((row, r) => (
                  <RosterRow
                    key={row.employee.publicId}
                    row={row}
                    r={r}
                    selected={selection && r >= selection.r1 && r <= selection.r2 ? [selection.c1, selection.c2] : null}
                    cursorColumn={cursor.r === r ? cursor.c : -1}
                    density={density}
                    wide={params.view !== "month"}
                    today={today}
                    shiftsById={shiftsById}
                    canWrite={canWrite}
                    onPointerDown={onCellPointerDown}
                    onPointerEnter={onCellPointerEnter}
                    onSelectRow={selectRow}
                  />
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Always in the layout, so selecting never resizes the grid under the pointer mid-drag. */}
      {canWrite && !calendar.isError && (
        <SelectionBar
          block={
            selection && rows.length > 0
              ? { rows: rows.slice(selection.r1, selection.r2 + 1), from: ymd(dates[selection.c1]), to: ymd(dates[selection.c2]) }
              : null
          }
          shifts={shifts.data}
          onDone={() => setAnchor(null)}
        />
      )}

      <Legend />

      {calendar.data && calendar.data.totalCount > 0 && (
        <Pagination
          page={calendar.data.page}
          pageSize={calendar.data.pageSize}
          totalCount={calendar.data.totalCount}
          totalPages={Math.ceil(calendar.data.totalCount / calendar.data.pageSize)}
          pageSizes={PAGE_SIZES}
          onPageChange={(page) => setParams({ page })}
          onPageSizeChange={(pageSize) => setParams({ pageSize: pageSize as (typeof PAGE_SIZES)[number], page: 1 })}
        />
      )}

      {bulkOpen && (
        <BulkRosterDialog
          shifts={shifts.data}
          defaults={{ departmentId: params.department, fromDate: ymd(period.from), toDate: ymd(period.to) }}
          onClose={() => setBulkOpen(false)}
        />
      )}
    </div>
  )
}

type RowProps = {
  row: TeamCalendarRow
  r: number
  /** The selected columns of this row, inclusive, or null. */
  selected: [number, number] | null
  cursorColumn: number
  density: "comfortable" | "compact"
  wide: boolean
  today: string
  shiftsById: Map<string, Shift>
  canWrite: boolean
  onPointerDown: (point: Point, event: PointerEvent) => void
  onPointerEnter: (point: Point) => void
  onSelectRow: (r: number) => void
}

/** One employee's row. Memoized: while dragging, only the rows the selection touches re-render. */
const RosterRow = memo(
  function RosterRow({
    row,
    r,
    selected,
    cursorColumn,
    density,
    wide,
    today,
    shiftsById,
    canWrite,
    onPointerDown,
    onPointerEnter,
    onSelectRow,
  }: RowProps) {
    const t = useTranslations("scheduling.roster")
    const height = density === "compact" ? "h-row-compact" : "h-row"
    return (
      <tr>
        <th scope="row" className={cn("sticky left-0 z-10 border-r border-b border-rule bg-surface p-0 text-left font-normal", height)}>
          <button
            type="button"
            tabIndex={-1}
            onClick={() => onSelectRow(r)}
            disabled={!canWrite}
            aria-label={t("selectEmployee", { name: row.employee.fullName })}
            className="flex h-full w-full min-w-0 items-center gap-1 px-1.5 text-left enabled:hover:bg-canvas"
          >
            <span className="w-8 shrink-0 truncate font-mono text-12 text-ink-muted">{row.employee.employeeCode}</span>
            <span className="min-w-0 flex-1">
              <span className="block truncate text-13 font-medium text-ink">{row.employee.fullName}</span>
              {density === "comfortable" && (
                <span className="block truncate text-12 text-ink-muted">
                  {row.defaultShift ? t("defaultShift", { code: row.defaultShift.code }) : t("noDefaultShift")}
                </span>
              )}
            </span>
          </button>
        </th>
        {row.days.map((day, c) => (
          <RosterCell
            key={day.date}
            day={day}
            employee={row.employee.fullName}
            beforeJoining={day.date < row.joiningDate}
            point={{ r, c }}
            selected={!!selected && c >= selected[0] && c <= selected[1]}
            isCursor={cursorColumn === c}
            height={height}
            wide={wide}
            isToday={day.date === today}
            nightShift={!!day.shift && !!shiftsById.get(day.shift.publicId)?.crossesMidnight}
            onPointerDown={onPointerDown}
            onPointerEnter={onPointerEnter}
          />
        ))}
      </tr>
    )
  },
  (a, b) =>
    a.row === b.row &&
    a.selected?.[0] === b.selected?.[0] &&
    a.selected?.[1] === b.selected?.[1] &&
    a.cursorColumn === b.cursorColumn &&
    a.density === b.density &&
    a.wide === b.wide &&
    a.today === b.today &&
    a.shiftsById === b.shiftsById &&
    a.canWrite === b.canWrite &&
    a.onPointerDown === b.onPointerDown &&
    a.onPointerEnter === b.onPointerEnter &&
    a.onSelectRow === b.onSelectRow &&
    a.r === b.r,
)

/**
 * One employee-day. What it shows comes from the API's calendar: a weekly off or holiday carries its
 * letter code and status tint (§4.2); a working day shows the effective shift's code, tinted when a roster
 * entry overrides the default, plain when it is the default.
 */
function RosterCell({
  day,
  employee,
  beforeJoining,
  point,
  selected,
  isCursor,
  height,
  wide,
  isToday,
  nightShift,
  onPointerDown,
  onPointerEnter,
}: {
  day: CalendarDay
  employee: string
  beforeJoining: boolean
  point: Point
  selected: boolean
  isCursor: boolean
  height: string
  wide: boolean
  isToday: boolean
  nightShift: boolean
  onPointerDown: (point: Point, event: PointerEvent) => void
  onPointerEnter: (point: Point) => void
}) {
  const t = useTranslations("scheduling.roster.cell")
  const status = day.dayType === "WeeklyOff" ? AttendanceStatuses.weeklyOff : day.dayType === "Holiday" ? AttendanceStatuses.holiday : null
  const date = format(parseISO(day.date), "d MMMM")

  const what = beforeJoining
    ? t("beforeJoining")
    : [
        day.dayType === "WeeklyOff" ? t("weeklyOff") : day.dayType === "Holiday" ? t("holiday", { name: day.holidayName ?? "" }) : null,
        day.shift ? t("shift", { code: day.shift.code, name: day.shift.name }) : t("noShift"),
        day.isRostered ? t("rostered") : t("default"),
      ]
        .filter(Boolean)
        .join(", ")

  return (
    <td
      role="gridcell"
      data-cell={`${point.r}:${point.c}`}
      tabIndex={isCursor ? 0 : -1}
      aria-selected={selected}
      aria-label={`${employee}, ${date}: ${what}`}
      title={day.holidayName ?? day.shift?.name ?? undefined}
      onPointerDown={(event) => onPointerDown(point, event)}
      onPointerEnter={() => onPointerEnter(point)}
      className={cn(
        "relative border-r border-b border-rule p-0.25 text-center outline-none focus-visible:z-10 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-brand",
        height,
        isToday && "bg-brand-soft/40",
        beforeJoining && "bg-canvas",
      )}
      style={beforeJoining ? { backgroundImage: "repeating-linear-gradient(135deg, var(--rule) 0 1px, transparent 1px 6px)" } : undefined}
    >
      {!beforeJoining && (
        <span
          aria-hidden="true"
          className={cn(
            "flex h-full w-full items-center justify-center gap-0.25 overflow-hidden rounded-sm border text-12 whitespace-nowrap",
            status
              ? cn(status.tint, status.border, status.text, "font-semibold")
              : day.isRostered
                ? "border-brand/40 bg-brand-soft font-mono font-semibold text-brand"
                : day.shift
                  ? "border-transparent font-mono text-ink-muted"
                  : "border-transparent text-ink-muted",
          )}
        >
          {status ? status.code : day.shift ? (
            <>
              {wide && nightShift && <MoonIcon className="size-1.25 shrink-0" />}
              <span className="truncate">{day.shift.code}</span>
            </>
          ) : (
            "—"
          )}
          {status && day.isRostered && <span className="absolute top-0.5 right-0.5 size-0.5 rounded-full bg-brand" />}
        </span>
      )}
      {selected && <span aria-hidden="true" className="pointer-events-none absolute inset-0 bg-brand/15 ring-2 ring-brand ring-inset" />}
    </td>
  )
}

function SkeletonRows({ count, columns, density, wide }: { count: number; columns: number; density: "comfortable" | "compact"; wide: boolean }) {
  const height = density === "compact" ? "h-row-compact" : "h-row"
  return Array.from({ length: count }, (_, r) => (
    <tr key={r} aria-hidden="true">
      <th className={cn("sticky left-0 z-10 border-r border-b border-rule bg-surface px-1.5", height)}>
        <Skeleton className="h-1.5 w-18" />
      </th>
      {Array.from({ length: columns }, (_, c) => (
        <td key={c} className={cn("border-r border-b border-rule p-0.5", height, wide ? "w-9" : "w-5")}>
          <Skeleton className="h-full w-full" />
        </td>
      ))}
    </tr>
  ))
}

/**
 * What to do with the selected block: one shift and day type for all of it (POST rosters/assign), or
 * back to each employee's default (POST rosters/clear). Either is one request however large the block.
 * With nothing selected it says how to select, and its buttons wait.
 */
function SelectionBar({
  block,
  shifts,
  onDone,
}: {
  block: { rows: TeamCalendarRow[]; from: string; to: string } | null
  shifts: Shift[] | undefined
  onDone: () => void
}) {
  const rows = block?.rows ?? []
  const from = block?.from ?? ""
  const to = block?.to ?? ""
  const t = useTranslations("scheduling.roster.selection")
  const tOverride = useTranslations("scheduling.roster.override")
  const errorText = useErrorText()
  const { assign, clear } = useRosterMutations()
  const [shiftId, setShiftId] = useState("")
  const [override, setOverride] = useState<OverrideChoice>("pattern")
  const [remarks, setRemarks] = useState("")
  const [error, setError] = useState<string | null>(null)
  const options = shiftOptions(shifts)
  const days = rows[0]?.days.filter((day) => day.date >= from && day.date <= to).length ?? 0
  const rostered = rows.reduce((sum, row) => sum + row.days.filter((day) => day.date >= from && day.date <= to && day.isRostered).length, 0)
  const employeeIds = rows.map((row) => row.employee.publicId)
  const range = !block
    ? ""
    : from === to
      ? format(parseISO(from), "d MMM")
      : `${format(parseISO(from), "d MMM")} – ${format(parseISO(to), "d MMM")}`
  const pending = assign.isPending || clear.isPending

  const run = async (action: "assign" | "clear") => {
    setError(null)
    try {
      if (action === "assign") {
        if (!shiftId) {
          setError(t("chooseShift"))
          return
        }
        const result = await assign.mutateAsync({
          employeeIds,
          fromDate: from,
          toDate: to,
          shiftId,
          isWeeklyOffOverride: toOverride(override),
          remarks: remarks.trim() || null,
        })
        const code = shifts?.find((s) => s.publicId === shiftId)?.code ?? ""
        toast.success(t("assigned", { code, employees: result.employeeCount, days: result.dayCount, created: result.created, updated: result.updated }))
      } else {
        const result = await clear.mutateAsync({ employeeIds, fromDate: from, toDate: to })
        toast.success(t("cleared", { removed: result.removed }))
      }
      onDone()
    } catch (failure) {
      setError(errorText(failure))
    }
  }

  return (
    <section
      aria-label={t("label")}
      className={cn(
        "flex flex-col gap-1 border-t-2 bg-surface px-3 py-1.5",
        block ? "border-brand shadow-[0_-4px_12px_rgb(20_24_31/0.06)]" : "border-rule",
      )}
    >
      <div className="flex flex-wrap items-end gap-1">
        <p className="mr-1 w-[180px] self-center text-14" aria-live="polite">
          {block ? (
            <>
              <span className="font-semibold">{t("count", { employees: rows.length, days })}</span>{" "}
              <span className="font-mono text-13 text-ink-muted">{range}</span>
            </>
          ) : (
            <span className="text-13 text-ink-muted">{t("empty")}</span>
          )}
        </p>
        <label className="flex flex-col gap-0.25 text-12 text-ink-muted">
          {t("shift")}
          <Select value={shiftId} onValueChange={setShiftId}>
            <SelectTrigger className="w-28" aria-label={t("shift")}>
              <SelectValue placeholder={t("chooseShift")}>{options.find((o) => o.value === shiftId)?.label}</SelectValue>
            </SelectTrigger>
            <SelectContent position="popper">
              {options.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </label>
        <label className="flex flex-col gap-0.25 text-12 text-ink-muted">
          {tOverride("label")}
          <Select value={override} onValueChange={(value) => setOverride(value as OverrideChoice)}>
            <SelectTrigger className="w-24" aria-label={tOverride("label")}>
              <SelectValue>{tOverride(override)}</SelectValue>
            </SelectTrigger>
            <SelectContent position="popper">
              {OVERRIDES.map((choice) => (
                <SelectItem key={choice} value={choice}>
                  {tOverride(choice)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </label>
        <label className="flex min-w-16 flex-1 flex-col gap-0.25 text-12 text-ink-muted">
          {t("remarks")}
          <Input value={remarks} onChange={(event) => setRemarks(event.target.value)} maxLength={300} placeholder={t("remarksPlaceholder")} />
        </label>
        <Button onClick={() => run("assign")} disabled={!block || pending}>
          {assign.isPending && <Spinner />}
          {t("assign")}
        </Button>
        <Button
          variant="outline"
          onClick={() => run("clear")}
          disabled={!block || pending || rostered === 0}
          title={block && rostered === 0 ? t("nothingToClear") : undefined}
        >
          {clear.isPending && <Spinner />}
          {t("clear", { count: rostered })}
        </Button>
        <Button variant="ghost" size="icon" onClick={onDone} disabled={!block} aria-label={t("deselect")}>
          <XIcon aria-hidden="true" />
        </Button>
      </div>
      <FormAlert>{error}</FormAlert>
      <p className="text-12 text-ink-muted">{tOverride(`hint.${override === "off" ? "off" : override === "working" ? "working" : "pattern"}`)} {t("joiningNote")}</p>
    </section>
  )
}

function Legend() {
  const t = useTranslations("scheduling.roster.legend")
  const off = AttendanceStatuses.weeklyOff
  const holiday = AttendanceStatuses.holiday
  return (
    <ul aria-label={t("label")} className="flex flex-wrap items-center gap-x-2 gap-y-0.5 border-t border-rule bg-surface px-3 py-0.75 text-12 text-ink-muted">
      <li className="flex items-center gap-0.5">
        <span className="flex h-2.5 w-4 items-center justify-center rounded-sm border border-brand/40 bg-brand-soft font-semibold text-brand">
          {t("code")}
        </span>
        {t("rostered")}
      </li>
      <li className="flex items-center gap-0.5">
        <span className="flex h-2.5 w-4 items-center justify-center">{t("code")}</span>
        {t("default")}
      </li>
      <li className="flex items-center gap-0.5">
        <span className={cn("flex h-2.5 w-4 items-center justify-center rounded-sm border font-semibold", off.tint, off.border, off.text)}>{off.code}</span>
        {t("weeklyOff")}
      </li>
      <li className="flex items-center gap-0.5">
        <span className={cn("flex h-2.5 w-4 items-center justify-center rounded-sm border font-semibold", holiday.tint, holiday.border, holiday.text)}>
          {holiday.code}
        </span>
        {t("holiday")}
      </li>
      <li className="flex items-center gap-0.5">
        <span
          className="h-2.5 w-4 rounded-sm bg-canvas"
          style={{ backgroundImage: "repeating-linear-gradient(135deg, var(--rule) 0 1px, transparent 1px 6px)" }}
        />
        {t("beforeJoining")}
      </li>
      <li className="ml-auto">{t("howTo")}</li>
    </ul>
  )
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max)
}


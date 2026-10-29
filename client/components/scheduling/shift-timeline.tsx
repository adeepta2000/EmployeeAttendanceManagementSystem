"use client"

import { MoonIcon } from "lucide-react"
import { useTranslations } from "next-intl"
import type { CSSProperties, ReactNode } from "react"

import { MINUTES_PER_DAY, dayOffset, formatHours, formatTime, shiftDuration } from "@/lib/shift-time"
import { cn } from "@/lib/utils"

/** Whatever the form holds right now: numbers may be missing while the user types. */
export type ShiftShape = {
  start: number | null
  end: number | null
  breakMinutes: number
  graceInMinutes: number
  graceOutMinutes: number
  halfDayThresholdMinutes: number
  fullDayMinutes: number
  punchWindowBeforeMinutes: number
  punchWindowAfterMinutes: number
}

// Patterns rather than colours: the status colours of §4.2 are attendance data and never decorate, so the
// timeline tells its parts apart by brand tints, hatching and labels.
const graceFill: CSSProperties = {
  backgroundImage: "repeating-linear-gradient(135deg, var(--brand) 0 1.5px, transparent 1.5px 5px)",
}
const breakFill: CSSProperties = {
  backgroundImage: "repeating-linear-gradient(45deg, var(--ink-muted) 0 1px, transparent 1px 4px)",
}

/**
 * A shift drawn on one horizontal bar (screen 29): the punch window around it, the start and end, the
 * grace windows that decide late-in and early-out, the break, and midnight for a night shift. Below it,
 * the worked-time thresholds. Everything it draws is also said in the legend's text, which is what
 * assistive technology reads; the drawing itself is hidden from it.
 */
export function ShiftTimeline({ shape }: { shape: ShiftShape }) {
  const t = useTranslations("scheduling.timeline")
  const { start, end } = shape
  const duration = start !== null && end !== null ? shiftDuration(start, end) : 0

  if (start === null || end === null || duration === 0) {
    return (
      <div className="flex h-[176px] items-center justify-center rounded-md border border-dashed border-rule bg-canvas px-2 text-center text-14 text-ink-muted">
        {start !== null && end !== null ? t("sameTimes") : t("enterTimes")}
      </div>
    )
  }

  // Offsets are minutes from midnight of the shift's start date; the end may be past 24:00.
  const shiftEnd = start + duration
  const before = clamp(shape.punchWindowBeforeMinutes, 0, MINUTES_PER_DAY)
  const after = clamp(shape.punchWindowAfterMinutes, 0, MINUTES_PER_DAY)
  const windowStart = start - before
  const windowEnd = shiftEnd + after
  const axisStart = Math.floor((windowStart - 30) / 60) * 60
  const axisEnd = Math.ceil((windowEnd + 30) / 60) * 60
  const span = axisEnd - axisStart
  const pct = (minutes: number) => `${((minutes - axisStart) / span) * 100}%`
  const width = (from: number, to: number) => `${(Math.max(0, to - from) / span) * 100}%`

  const graceIn = clamp(shape.graceInMinutes, 0, duration)
  const graceOut = clamp(shape.graceOutMinutes, 0, duration)
  const breakMinutes = clamp(shape.breakMinutes, 0, duration)
  const breakStart = start + (duration - breakMinutes) / 2
  const crossesMidnight = shiftEnd > MINUTES_PER_DAY
  const midnights = [0, MINUTES_PER_DAY, 2 * MINUTES_PER_DAY].filter((m) => m > axisStart && m < axisEnd)

  const hourStep = span > 16 * 60 ? 180 : span > 8 * 60 ? 120 : 60
  const ticks: number[] = []
  // A label is centred on its tick, so ticks at the very ends would hang outside the axis.
  for (let m = Math.ceil(axisStart / hourStep) * hourStep; m <= axisEnd; m += hourStep) {
    if (m - axisStart >= span * 0.03 && axisEnd - m >= span * 0.03) ticks.push(m)
  }

  return (
    <div aria-hidden="true" className="select-none">
      {/* Start and end, above the bar. */}
      <div className="relative h-4 text-12">
        <Marker at={pct(start)} align="start" strong>
          {t("start")} <Time minutes={start} />
        </Marker>
        <Marker at={pct(shiftEnd)} align="end" strong>
          {t("end")} <Time minutes={shiftEnd} />
        </Marker>
      </div>

      <div className="relative h-7">
        {/* The punch window: punches here are attributed to this shift. */}
        <div
          className="absolute inset-y-0 rounded-md border border-dashed border-ink-muted/50 bg-canvas"
          style={{ left: pct(windowStart), width: width(windowStart, windowEnd) }}
        />
        {/* The shift itself. */}
        <div
          className="absolute inset-y-1 overflow-hidden rounded-sm border border-brand bg-brand-soft"
          style={{ left: pct(start), width: width(start, shiftEnd) }}
        >
          {graceIn > 0 && (
            <div className="absolute inset-y-0 left-0 border-r border-brand/60" style={{ ...graceFill, width: `${(graceIn / duration) * 100}%`, opacity: 0.55 }} />
          )}
          {graceOut > 0 && (
            <div className="absolute inset-y-0 right-0 border-l border-brand/60" style={{ ...graceFill, width: `${(graceOut / duration) * 100}%`, opacity: 0.55 }} />
          )}
          {breakMinutes > 0 && (
            <div
              className="absolute inset-y-1 rounded-pill border border-ink-muted/40 bg-surface"
              style={{ ...breakFill, left: `${((breakStart - start) / duration) * 100}%`, width: `${(breakMinutes / duration) * 100}%` }}
            />
          )}
        </div>
        {midnights.map((m) => (
          <div key={m} className="absolute -inset-y-0.5 border-l-2 border-dashed border-ink" style={{ left: pct(m) }}>
            <span className="absolute -top-0.5 left-0.5 flex items-center gap-0.25 rounded-pill bg-surface px-0.25 text-12 font-medium whitespace-nowrap text-ink">
              <MoonIcon className="size-1.5" />
              {t("midnight")}
            </span>
          </div>
        ))}
      </div>

      {/* Where lateness and early-outs begin, below the bar. */}
      <div className="relative h-4 text-12">
        {graceIn > 0 && (
          <Marker at={pct(start + graceIn)} align="start">
            {t("lateAfter")} <Time minutes={start + graceIn} />
          </Marker>
        )}
        {graceOut > 0 && (
          <Marker at={pct(shiftEnd - graceOut)} align="end">
            {t("earlyBefore")} <Time minutes={shiftEnd - graceOut} />
          </Marker>
        )}
      </div>

      {/* The hour axis, and the edges of the punch window. */}
      <div className="relative h-3 border-t border-rule text-12 text-ink-muted">
        {ticks.map((m) => (
          <span key={m} className="absolute top-0 -translate-x-1/2 font-mono" style={{ left: pct(m) }}>
            <span className="mx-auto block h-0.5 w-px bg-rule" />
            {formatTime(m)}
          </span>
        ))}
      </div>
      <div className="relative mt-0.5 h-2.5 text-12 text-ink-muted">
        <Marker at={pct(windowStart)} align="start" muted>
          {t("windowFrom")} <Time minutes={windowStart} />
        </Marker>
        <Marker at={pct(windowEnd)} align="end" muted>
          {t("windowUntil")} <Time minutes={windowEnd} />
        </Marker>
      </div>

      {crossesMidnight && <span className="sr-only">{t("crossesMidnight")}</span>}
    </div>
  )
}

/** The worked-time scale: what a day's worked minutes (BR-02) make it, from nothing to the whole shift. */
export function WorkedTimeScale({ shape }: { shape: ShiftShape }) {
  const t = useTranslations("scheduling.timeline")
  const { start, end } = shape
  const duration = start !== null && end !== null ? shiftDuration(start, end) : 0
  const available = duration - clamp(shape.breakMinutes, 0, duration)
  if (duration === 0 || available <= 0) return null

  const halfDay = clamp(shape.halfDayThresholdMinutes, 0, available)
  const fullDay = clamp(shape.fullDayMinutes, 0, available)
  const pct = (minutes: number) => `${(minutes / available) * 100}%`

  return (
    <div aria-hidden="true" className="select-none">
      <div className="relative flex h-3 overflow-hidden rounded-sm border border-rule text-12">
        <div className="flex items-center border-r border-rule bg-canvas px-0.5 text-ink-muted" style={{ width: pct(halfDay) }}>
          <span className="truncate">{t("halfDayBand")}</span>
        </div>
        <div className="flex flex-1 items-center bg-brand-soft px-0.5 text-brand">
          <span className="truncate">{t("presentBand")}</span>
        </div>
        <div className="absolute inset-y-0 border-l-2 border-brand" style={{ left: pct(fullDay) }} />
      </div>
      <div className="relative mt-0.25 h-2.5 text-12 text-ink-muted">
        <Marker at="0%" align="start" muted>
          <span className="font-mono">0:00</span>
        </Marker>
        {halfDay > 0 && halfDay < available && (
          <Marker at={pct(halfDay)} align={halfDay / available > 0.5 ? "end" : "start"} muted>
            {t("halfDayMark")} <span className="font-mono">{formatHours(halfDay)}</span>
          </Marker>
        )}
        <Marker at={pct(available)} align="end" muted>
          <span className="font-mono" title={t("availableMark")}>
            {formatHours(available)}
          </span>
        </Marker>
      </div>
      <div className="relative h-2.5 text-12 font-medium text-brand">
        <Marker at={pct(fullDay)} align={fullDay / available > 0.6 ? "end" : "start"}>
          {t("fullDayMark")} <span className="font-mono">{formatHours(fullDay)}</span>
        </Marker>
      </div>
    </div>
  )
}

/**
 * A compact 24-hour bar for the shift list: where in the day the shift sits. A night shift wraps, so it
 * shows as two pieces, the second continuing from midnight.
 */
export function ShiftDayBar({ start, end, className }: { start: number; end: number; className?: string }) {
  const duration = shiftDuration(start, end)
  const pieces =
    start + duration <= MINUTES_PER_DAY
      ? [[start, start + duration]]
      : [
          [start, MINUTES_PER_DAY],
          [0, start + duration - MINUTES_PER_DAY],
        ]
  return (
    <div aria-hidden="true" className={cn("relative h-1.5 w-full rounded-pill bg-canvas ring-1 ring-rule ring-inset", className)}>
      {[6, 12, 18].map((hour) => (
        <span key={hour} className="absolute inset-y-0 w-px bg-rule" style={{ left: `${(hour / 24) * 100}%` }} />
      ))}
      {pieces.map(([from, to]) => (
        <span
          key={from}
          className="absolute inset-y-0 rounded-pill bg-brand"
          style={{ left: `${(from / MINUTES_PER_DAY) * 100}%`, width: `${((to - from) / MINUTES_PER_DAY) * 100}%` }}
        />
      ))}
    </div>
  )
}

function Marker({
  at,
  align,
  strong,
  muted,
  children,
}: {
  at: string
  align: "start" | "end"
  strong?: boolean
  muted?: boolean
  children: ReactNode
}) {
  return (
    <span
      className={cn(
        "absolute top-0 flex items-center gap-0.5 whitespace-nowrap",
        align === "start" ? "border-l pl-0.5" : "-translate-x-full border-r pr-0.5",
        strong ? "border-brand font-semibold text-ink" : muted ? "border-ink-muted/50" : "border-brand/60 text-ink",
      )}
      style={{ left: at }}
    >
      {children}
    </span>
  )
}

/** A wall-clock time in mono, with "+1" when it falls on the next day and "−1" on the day before. */
export function Time({ minutes }: { minutes: number }) {
  const t = useTranslations("scheduling.timeline")
  const offset = dayOffset(minutes)
  return (
    <span className="font-mono tabular">
      {formatTime(minutes)}
      {offset !== 0 && (
        <>
          <sup aria-hidden="true" className="ml-0.25 text-12 font-semibold text-ink-muted" title={offset > 0 ? t("nextDay") : t("previousDay")}>
            {offset > 0 ? `+${offset}` : `−${-offset}`}
          </sup>
          <span className="sr-only"> ({offset > 0 ? t("nextDay") : t("previousDay")})</span>
        </>
      )}
    </span>
  )
}

function clamp(value: number, min: number, max: number) {
  return Number.isFinite(value) ? Math.min(Math.max(value, min), max) : min
}

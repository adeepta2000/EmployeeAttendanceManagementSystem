// Shift times are wall-clock minutes of the day (0–1439), not instants, so this is integer arithmetic on
// minutes rather than Date arithmetic. Dates still go through date-fns (lib/dates.ts).

export const MINUTES_PER_DAY = 24 * 60

/** "09:30" or "09:30:00" (the API's TimeOnly) to minutes after midnight; null if it isn't a time. */
export function parseTime(value: string | null | undefined): number | null {
  const match = /^([01]\d|2[0-3]):([0-5]\d)(?::00)?$/.exec(value?.trim() ?? "")
  return match ? Number(match[1]) * 60 + Number(match[2]) : null
}

/** Minutes after midnight to "HH:mm", wrapping past midnight (1500 is "01:00"). */
export function formatTime(minutes: number): string {
  const wrapped = ((minutes % MINUTES_PER_DAY) + MINUTES_PER_DAY) % MINUTES_PER_DAY
  return `${String(Math.floor(wrapped / 60)).padStart(2, "0")}:${String(wrapped % 60).padStart(2, "0")}`
}

/** How many days after the shift's start date a minute offset falls on: -1, 0, +1. */
export function dayOffset(minutes: number): number {
  return Math.floor(minutes / MINUTES_PER_DAY)
}

/** A shift's length: the end is on the next day when it is earlier in the day than the start (BR-12). */
export function shiftDuration(start: number, end: number): number {
  return (end - start + MINUTES_PER_DAY) % MINUTES_PER_DAY
}

/** A duration as "7:30", for tables and timelines (JetBrains Mono, §4.2). */
export function formatHours(minutes: number): string {
  const sign = minutes < 0 ? "−" : ""
  const abs = Math.abs(minutes)
  return `${sign}${Math.floor(abs / 60)}:${String(abs % 60).padStart(2, "0")}`
}

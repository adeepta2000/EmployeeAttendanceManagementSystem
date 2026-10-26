import { format, isValid, parseISO } from "date-fns"
import { formatInTimeZone } from "date-fns-tz"

// Dates go through date-fns, never raw Date arithmetic (§2.4). Business dates are Asia/Dhaka.

export const BUSINESS_TIME_ZONE = "Asia/Dhaka"

/**
 * A DateOnly from the API (`yyyy-MM-dd`) for display: `04 Sep 2026`, the format of §4.3. It is a calendar
 * date, not an instant, so no time-zone conversion applies.
 */
export function formatDate(value: string | null | undefined): string {
  if (!value) return ""
  const date = parseISO(value)
  return isValid(date) ? format(date, "dd MMM yyyy") : value
}

/**
 * Today's business date as `yyyy-MM-dd`, for form defaults only. The server decides every date that
 * matters; the client clock is never trusted ("Time").
 */
export function todayInBusinessZone(): string {
  return formatInTimeZone(new Date(), BUSINESS_TIME_ZONE, "yyyy-MM-dd")
}

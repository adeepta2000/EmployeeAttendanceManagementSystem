import { useTranslations } from "next-intl"

import { AttendanceStatuses, type AttendanceStatus } from "@/lib/status"
import { cn } from "@/lib/utils"

/**
 * An attendance status as data (§4.2): the letter code on its status tint, with the full label for
 * assistive tech, and optionally shown. 4px radius, the pill radius of §4.2.
 */
export function StatusPill({
  status,
  showLabel = false,
  className,
}: {
  status: AttendanceStatus
  showLabel?: boolean
  className?: string
}) {
  const t = useTranslations("status")
  const style = AttendanceStatuses[status]
  return (
    <span
      className={cn(
        "inline-flex h-2.5 items-center gap-0.5 rounded-pill border px-0.5 text-12 font-semibold",
        style.text,
        style.tint,
        style.border,
        className,
      )}
    >
      <span aria-hidden={!showLabel ? undefined : true}>{style.code}</span>
      {showLabel ? <span className="font-medium">{t(status)}</span> : <span className="sr-only">{t(status)}</span>}
    </span>
  )
}

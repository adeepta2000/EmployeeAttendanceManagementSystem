import { useTranslations } from "next-intl"

import { cn } from "@/lib/utils"

/**
 * Whether a record is active. Deliberately neutral: the status colours of §4.2 are attendance data and
 * never appear for anything else, so this is told apart by its text and a muted style only.
 */
export function ActiveBadge({ active, className }: { active: boolean; className?: string }) {
  const t = useTranslations("common")
  return (
    <span
      className={cn(
        "inline-flex h-2.5 items-center rounded-pill border px-0.75 text-12 font-medium",
        active ? "border-rule bg-surface text-ink" : "border-transparent bg-canvas text-ink-muted",
        className,
      )}
    >
      {active ? t("active") : t("inactive")}
    </span>
  )
}

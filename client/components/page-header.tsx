import type { ReactNode } from "react"

import { cn } from "@/lib/utils"

/** A screen's title row: the 20px page title (§4.2) on the left, its actions on the right. */
export function PageHeader({
  title,
  description,
  actions,
  className,
  children,
}: {
  title: ReactNode
  description?: ReactNode
  actions?: ReactNode
  className?: string
  children?: ReactNode
}) {
  return (
    <div className={cn("flex flex-col gap-1 border-b border-rule bg-surface px-3 pt-2", children ? "pb-0" : "pb-2", className)}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <h1 className="text-20 font-semibold text-ink">{title}</h1>
          {description && <div className="mt-0.25 text-14 text-ink-muted">{description}</div>}
        </div>
        {actions && <div className="flex shrink-0 items-center gap-1">{actions}</div>}
      </div>
      {children}
    </div>
  )
}

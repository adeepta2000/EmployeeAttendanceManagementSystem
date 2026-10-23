import { CircleAlertIcon } from "lucide-react"
import type { ReactNode } from "react"

import { cn } from "@/lib/utils"

/**
 * A form-level message: what went wrong and what to do (§4.5). role="alert" makes screen readers announce
 * it the moment it appears (§4.7).
 */
export function FormAlert({ children, className }: { children: ReactNode; className?: string }) {
  if (!children) return null
  return (
    <div
      role="alert"
      className={cn(
        "flex items-start gap-1 rounded-md border border-danger/30 bg-danger/5 px-1.5 py-1 text-14 text-danger",
        className,
      )}
    >
      <CircleAlertIcon aria-hidden="true" className="mt-0.25 size-2 shrink-0" />
      <div>{children}</div>
    </div>
  )
}

/** A neutral notice above a form, e.g. "You've signed out." */
export function FormNotice({ children, className }: { children: ReactNode; className?: string }) {
  if (!children) return null
  return (
    <div
      role="status"
      className={cn("rounded-md border border-rule bg-brand-soft px-1.5 py-1 text-14 text-ink", className)}
    >
      {children}
    </div>
  )
}

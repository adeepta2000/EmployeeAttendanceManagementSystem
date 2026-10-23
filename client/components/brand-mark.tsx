import { cn } from "@/lib/utils"

/** The product mark: a solid brand square with the initial, plus the name unless `compact`. */
export function BrandMark({ name, compact, className }: { name: string; compact?: boolean; className?: string }) {
  return (
    <span className={cn("flex items-center gap-1 font-semibold text-ink", className)}>
      <span
        aria-hidden="true"
        className="grid size-3 shrink-0 place-items-center rounded-pill bg-brand text-12 font-bold text-white"
      >
        N
      </span>
      <span className={cn("truncate text-14", compact && "sr-only")}>{name}</span>
    </span>
  )
}

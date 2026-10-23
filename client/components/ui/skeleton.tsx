// tokens: 8px spacing, spec type scale (scripts/shadcn-8px.mjs)
import { cn } from "@/lib/utils"

function Skeleton({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="skeleton"
      className={cn("animate-pulse rounded-md bg-rule/70", className)}
      {...props}
    />
  )
}

export { Skeleton }

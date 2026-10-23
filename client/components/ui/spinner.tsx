// tokens: 8px spacing, spec type scale (scripts/shadcn-8px.mjs)
import { cn } from "@/lib/utils"
import { Loader2Icon } from "lucide-react"

function Spinner({ className, ...props }: React.ComponentProps<"svg">) {
  return (
    <Loader2Icon data-slot="spinner" role="status" aria-label="Loading" className={cn("size-2 animate-spin", className)} {...props} />
  )
}

export { Spinner }

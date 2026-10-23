"use client"

import { Rows3Icon, Rows4Icon } from "lucide-react"
import { useTranslations } from "next-intl"

import { useDensity } from "@/components/shell/density"
import { Button } from "@/components/ui/button"
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip"

/** Comfortable (40px) or compact (32px) rows, remembered in a cookie (§4.2). */
export function DensityToggle() {
  const t = useTranslations("table.density")
  const { density, setDensity } = useDensity()
  const compact = density === "compact"

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          variant="outline"
          size="icon"
          aria-pressed={compact}
          aria-label={t("compact")}
          onClick={() => setDensity(compact ? "comfortable" : "compact")}
        >
          {compact ? <Rows4Icon aria-hidden="true" /> : <Rows3Icon aria-hidden="true" />}
        </Button>
      </TooltipTrigger>
      <TooltipContent>{compact ? t("switchToComfortable") : t("switchToCompact")}</TooltipContent>
    </Tooltip>
  )
}

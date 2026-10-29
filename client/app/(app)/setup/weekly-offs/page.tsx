import type { Metadata } from "next"
import { getTranslations } from "next-intl/server"
import { Suspense } from "react"

import { DesktopOnly } from "@/components/desktop-only"
import { WeeklyOffPatterns } from "@/components/scheduling/weekly-off-patterns"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("scheduling.patterns")
  return { title: t("title") }
}

/** Screen 30. */
export default function WeeklyOffPatternsPage() {
  return (
    <DesktopOnly>
      {/* The open pattern and the list's filter live in the URL (useSearchParams). */}
      <Suspense>
        <WeeklyOffPatterns />
      </Suspense>
    </DesktopOnly>
  )
}

import type { Metadata } from "next"
import { getTranslations } from "next-intl/server"
import { Suspense } from "react"

import { DesktopOnly } from "@/components/desktop-only"
import { HolidayCalendar } from "@/components/scheduling/holiday-calendar"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("scheduling.holidays")
  return { title: t("title") }
}

/** Screen 32. */
export default function HolidaysPage() {
  return (
    <DesktopOnly>
      {/* The year and location live in the URL (useSearchParams). */}
      <Suspense>
        <HolidayCalendar />
      </Suspense>
    </DesktopOnly>
  )
}

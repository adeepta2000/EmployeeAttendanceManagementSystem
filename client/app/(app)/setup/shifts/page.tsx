import type { Metadata } from "next"
import { getTranslations } from "next-intl/server"
import { Suspense } from "react"

import { DesktopOnly } from "@/components/desktop-only"
import { ShiftList } from "@/components/scheduling/shift-list"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("scheduling.shifts")
  return { title: t("title") }
}

/** Screen 29. */
export default function ShiftsPage() {
  return (
    <DesktopOnly>
      {/* The list reads its filters from the URL (useSearchParams). */}
      <Suspense>
        <ShiftList />
      </Suspense>
    </DesktopOnly>
  )
}

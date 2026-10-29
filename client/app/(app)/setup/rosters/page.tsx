import type { Metadata } from "next"
import { getTranslations } from "next-intl/server"
import { Suspense } from "react"

import { DesktopOnly } from "@/components/desktop-only"
import { TeamRoster } from "@/components/scheduling/team-roster"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("scheduling.roster")
  return { title: t("title") }
}

/** Screen 16. */
export default function RostersPage() {
  return (
    <DesktopOnly>
      {/* The department, period and page live in the URL (useSearchParams). */}
      <Suspense>
        <TeamRoster />
      </Suspense>
    </DesktopOnly>
  )
}

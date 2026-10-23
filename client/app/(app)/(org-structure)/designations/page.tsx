import type { Metadata } from "next"
import { getTranslations } from "next-intl/server"
import { Suspense } from "react"

import { Designations } from "@/components/org/designations"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("org.tabs")
  return { title: t("designations") }
}

export default function DesignationsPage() {
  return (
    <Suspense>
      <Designations />
    </Suspense>
  )
}

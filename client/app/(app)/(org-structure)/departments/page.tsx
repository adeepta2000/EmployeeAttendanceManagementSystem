import type { Metadata } from "next"
import { getTranslations } from "next-intl/server"
import { Suspense } from "react"

import { Departments } from "@/components/org/departments"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("org.tabs")
  return { title: t("departments") }
}

export default function DepartmentsPage() {
  return (
    <Suspense>
      <Departments />
    </Suspense>
  )
}

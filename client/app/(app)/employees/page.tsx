import type { Metadata } from "next"
import { getTranslations } from "next-intl/server"
import { Suspense } from "react"

import { DesktopOnly } from "@/components/desktop-only"
import { EmployeeList } from "@/components/employees/employee-list"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("employees.list")
  return { title: t("title") }
}

export default function EmployeesPage() {
  return (
    <DesktopOnly>
      {/* The list reads its filters from the URL (useSearchParams). */}
      <Suspense>
        <EmployeeList />
      </Suspense>
    </DesktopOnly>
  )
}

import type { Metadata } from "next"
import { redirect } from "next/navigation"
import { getTranslations } from "next-intl/server"
import { Suspense } from "react"

import { DesktopOnly } from "@/components/desktop-only"
import { NewEmployee } from "@/components/employees/employee-detail"
import { getSession } from "@/lib/bff/server-session"
import { Permissions } from "@/lib/permissions"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("employees.detail")
  return { title: t("newTitle") }
}

export default async function NewEmployeePage() {
  // A convenience only: the API refuses the save without employee.write anyway.
  const session = await getSession()
  if (!session?.permissions.includes(Permissions.Employee.Write)) redirect("/employees")

  return (
    <DesktopOnly>
      <Suspense>
        <NewEmployee />
      </Suspense>
    </DesktopOnly>
  )
}

import type { Metadata } from "next"
import { notFound } from "next/navigation"
import { getTranslations } from "next-intl/server"
import { Suspense } from "react"
import { z } from "zod"

import { DesktopOnly } from "@/components/desktop-only"
import { EmployeeDetail } from "@/components/employees/employee-detail"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("employees.detail")
  return { title: t("pageTitle") }
}

const publicId = z.guid()

/** Screen 27. Records are addressed by PublicId only (CLAUDE.md, "Public keys"). */
export default async function EmployeePage({ params }: PageProps<"/employees/[publicId]">) {
  const { publicId: id } = await params
  if (!publicId.safeParse(id).success) notFound()

  return (
    <DesktopOnly>
      <Suspense>
        <EmployeeDetail id={id} />
      </Suspense>
    </DesktopOnly>
  )
}

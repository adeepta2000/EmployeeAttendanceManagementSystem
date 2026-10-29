import type { Metadata } from "next"
import { redirect } from "next/navigation"
import { getTranslations } from "next-intl/server"

import { DesktopOnly } from "@/components/desktop-only"
import { ShiftEditor } from "@/components/scheduling/shift-editor"
import { getSession } from "@/lib/bff/server-session"
import { Permissions } from "@/lib/permissions"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("scheduling.shifts")
  return { title: t("addTitle") }
}

export default async function NewShiftPage() {
  // A convenience only: the API refuses the save without shift.write anyway.
  const session = await getSession()
  if (!session?.permissions.includes(Permissions.Shift.Write)) redirect("/setup/shifts")

  return (
    <DesktopOnly>
      <ShiftEditor />
    </DesktopOnly>
  )
}

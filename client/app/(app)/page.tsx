import { redirect } from "next/navigation"

import { getSession } from "@/lib/bff/server-session"
import { navigationFor } from "@/lib/nav"

/**
 * Home. Dashboards arrive in Phase 7, so for now the first screen in the user's menu that exists; an
 * employee with no such screen gets their own record.
 */
export default async function HomePage() {
  const session = await getSession()
  if (!session) redirect("/login")

  const first = navigationFor(session.permissions)
    .flatMap((group) => group.items)
    .find((item) => item.available)
  if (first) redirect(first.href)
  if (session.employeeId) redirect(`/employees/${session.employeeId}`)
  redirect("/change-password")
}

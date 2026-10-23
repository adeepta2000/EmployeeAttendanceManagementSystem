import { cookies } from "next/headers"
import { redirect } from "next/navigation"
import type { ReactNode } from "react"

import { AppShell } from "@/components/shell/app-shell"
import { DensityProvider } from "@/components/shell/density"
import { SessionProvider } from "@/components/shell/session-context"
import type { ShellUser } from "@/components/shell/user-menu"
import type { Employee } from "@/lib/api/types"
import { PreferenceCookies } from "@/lib/bff/cookies"
import { getSession, serverGet } from "@/lib/bff/server-session"
import type { Session } from "@/lib/bff/session"
import { navigationFor } from "@/lib/nav"

/** Every signed-in screen sits in the shell. proxy.ts has already turned away requests with no session. */
export default async function AppLayout({ children }: { children: ReactNode }) {
  const session = await getSession()
  if (!session) redirect("/login")

  const store = await cookies()
  const collapsed = store.get(PreferenceCookies.sidebar)?.value === "collapsed"
  const density = store.get(PreferenceCookies.density)?.value === "compact" ? "compact" : "comfortable"

  // Filtered here, on the server: links the user can't follow are never sent to the browser.
  const groups = navigationFor(session.permissions)
  const canSearchEmployees = groups.some((group) => group.items.some((item) => item.id === "employees"))

  return (
    <SessionProvider
      session={{ userId: session.userId, employeeId: session.employeeId, permissions: session.permissions }}
    >
      <DensityProvider initial={density}>
        <AppShell
          groups={groups}
          initialCollapsed={collapsed}
          canSearchEmployees={canSearchEmployees}
          // Not awaited: the shell renders at once and the name streams into the user menu.
          userPromise={loadShellUser(session)}
        >
          {children}
        </AppShell>
      </DensityProvider>
    </SessionProvider>
  )
}

async function loadShellUser(session: Session): Promise<ShellUser> {
  if (!session.employeeId) return { name: null, employeeCode: null, employeeId: null }
  const employee = await serverGet<Employee>(`/api/employees/${session.employeeId}`)
  return {
    name: employee?.fullName ?? null,
    employeeCode: employee?.employeeCode ?? null,
    employeeId: session.employeeId,
  }
}

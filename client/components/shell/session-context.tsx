"use client"

import { createContext, use, useMemo, type ReactNode } from "react"

import type { Permission } from "@/lib/permissions"

/** What client components know about the signed-in user. No tokens: those never leave the server. */
export type ClientSession = {
  userId: string
  employeeId: string | null
  permissions: string[]
}

type SessionValue = ClientSession & { can: (permission: Permission) => boolean }

const SessionContext = createContext<SessionValue | null>(null)

export function SessionProvider({ session, children }: { session: ClientSession; children: ReactNode }) {
  const value = useMemo(() => {
    const granted = new Set(session.permissions)
    return { ...session, can: (permission: Permission) => granted.has(permission) }
  }, [session])
  return <SessionContext value={value}>{children}</SessionContext>
}

/**
 * The signed-in user. `can()` only decides what to show; the API authorizes every request itself.
 */
export function useSession(): SessionValue {
  const value = use(SessionContext)
  if (!value) throw new Error("useSession must be used inside the app shell")
  return value
}

"use client"

import { createContext, use, useState, type ReactNode } from "react"

import { PreferenceCookies, writePreferenceCookie } from "@/lib/cookie-names"

/** Table row height (§4.2): 40px comfortable, or the 32px compact rows HR prefers. */
export type Density = "comfortable" | "compact"

const DensityContext = createContext<{ density: Density; setDensity: (density: Density) => void } | null>(null)

export function DensityProvider({ initial, children }: { initial: Density; children: ReactNode }) {
  const [density, setState] = useState(initial)
  const setDensity = (next: Density) => {
    setState(next)
    writePreferenceCookie(PreferenceCookies.density, next)
  }
  return <DensityContext value={{ density, setDensity }}>{children}</DensityContext>
}

export function useDensity() {
  const value = use(DensityContext)
  if (!value) throw new Error("useDensity must be used inside DensityProvider")
  return value
}

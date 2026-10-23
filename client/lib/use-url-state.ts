"use client"

import { usePathname, useSearchParams } from "next/navigation"
import { useCallback, useMemo } from "react"
import type { z } from "zod"

type Values = Record<string, string | number | undefined>

/**
 * A screen's filters, sort and page, kept in the URL query string (spec §4.5) so a view can be
 * bookmarked or shared. The schema parses whatever is in the URL (falling back to defaults for anything
 * malformed); values equal to their default are left out, so links stay short.
 *
 * Updates use the History API, which Next.js syncs with useSearchParams without a server round trip.
 */
export function useUrlState<S extends z.ZodType<Values>>(schema: S) {
  const searchParams = useSearchParams()
  const pathname = usePathname()

  const defaults = useMemo(() => schema.parse({}) as z.output<S>, [schema])
  const state = useMemo(
    () => schema.parse(Object.fromEntries(searchParams.entries())) as z.output<S>,
    [schema, searchParams],
  )

  const setState = useCallback(
    (patch: Partial<z.output<S>>, options: { replace?: boolean } = {}) => {
      const next = { ...state, ...patch } as Values
      const params = new URLSearchParams()
      for (const [key, value] of Object.entries(next)) {
        if (value === undefined || value === "" || value === (defaults as Values)[key]) continue
        params.set(key, String(value))
      }
      const query = params.toString()
      const url = query ? `${pathname}?${query}` : pathname
      if (options.replace) window.history.replaceState(null, "", url)
      else window.history.pushState(null, "", url)
    },
    [state, defaults, pathname],
  )

  return [state, setState, defaults] as const
}

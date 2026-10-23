import type { NextRequest } from "next/server"

import { CookieNames } from "./cookies"

/** The header the client sends the anti-forgery token in (lib/api/client.ts). */
export const CSRF_HEADER = "x-csrf-token"

export function newCsrfToken(): string {
  return Buffer.from(crypto.getRandomValues(new Uint8Array(32))).toString("base64url")
}

/**
 * Anti-forgery check for a state-changing Route Handler call (spec §2.6, CSRF). SameSite=Lax cookies
 * already keep cross-site POSTs from carrying the session; on top of that:
 *
 * 1. The request must come from this origin (`Origin`, or `Sec-Fetch-Site` where Origin is absent).
 * 2. The `X-CSRF-Token` header must match the `nw_csrf` cookie (double submit). Another site can make the
 *    browser send the cookie, but can't read it to put it in the header.
 */
export function isSameOriginRequest(request: NextRequest): boolean {
  const fetchSite = request.headers.get("sec-fetch-site")
  if (fetchSite && fetchSite !== "same-origin") return false

  const origin = request.headers.get("origin")
  if (origin) {
    const host = request.headers.get("x-forwarded-host") ?? request.headers.get("host")
    try {
      if (new URL(origin).host !== host) return false
    } catch {
      return false
    }
  } else if (!fetchSite) {
    // Neither header: not a browser fetch from this app.
    return false
  }

  const cookie = request.cookies.get(CookieNames.csrf)?.value
  const header = request.headers.get(CSRF_HEADER)
  return !!cookie && !!header && timingSafeEqual(cookie, header)
}

function timingSafeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false
  let difference = 0
  for (let i = 0; i < a.length; i++) difference |= a.charCodeAt(i) ^ b.charCodeAt(i)
  return difference === 0
}

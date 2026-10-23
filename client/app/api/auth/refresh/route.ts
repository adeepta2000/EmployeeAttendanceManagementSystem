import { NextResponse, type NextRequest } from "next/server"

import { clientInfo } from "@/lib/bff/client-info"
import { clearSessionCookies, CookieNames, writeSessionCookies } from "@/lib/bff/cookies"
import { isSameOriginRequest } from "@/lib/bff/csrf"
import { BffErrors } from "@/lib/bff/problem"
import { refreshSession } from "@/lib/bff/refresh"

/**
 * Renews the session on request, e.g. to pick up permission changes, which take effect at the next
 * refresh. Data calls don't need it: /api/backend and proxy.ts refresh on their own when needed.
 */
export async function POST(request: NextRequest) {
  if (!isSameOriginRequest(request)) return BffErrors.forbiddenOrigin()

  const refreshToken = request.cookies.get(CookieNames.refreshToken)?.value
  if (!refreshToken) return BffErrors.sessionExpired()

  const outcome = await refreshSession(refreshToken, clientInfo(request.headers))
  if (outcome.kind === "unavailable") return BffErrors.unavailable()
  if (outcome.kind === "rejected") {
    const response = BffErrors.sessionExpired()
    clearSessionCookies(response.cookies)
    return response
  }

  const response = NextResponse.json(
    { mustChangePassword: outcome.tokens.mustChangePassword },
    { headers: { "Cache-Control": "no-store" } },
  )
  writeSessionCookies(response.cookies, outcome.tokens)
  return response
}

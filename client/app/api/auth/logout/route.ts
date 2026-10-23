import { NextResponse, type NextRequest } from "next/server"

import { clientInfo } from "@/lib/bff/client-info"
import { clearSessionCookies, CookieNames } from "@/lib/bff/cookies"
import { isSameOriginRequest } from "@/lib/bff/csrf"
import { BffErrors } from "@/lib/bff/problem"
import { callUpstream, newCorrelationId } from "@/lib/bff/upstream"

/**
 * Signs out: revokes the session's refresh-token family at the API and clears the cookies. The cookies
 * are cleared even if the API can't be reached, so this browser is signed out either way.
 */
export async function POST(request: NextRequest) {
  if (!isSameOriginRequest(request)) return BffErrors.forbiddenOrigin()

  const refreshToken = request.cookies.get(CookieNames.refreshToken)?.value
  if (refreshToken) {
    try {
      const upstream = await callUpstream({
        method: "POST",
        path: "/api/auth/logout",
        client: clientInfo(request.headers),
        correlationId: newCorrelationId(),
        body: { refreshToken },
      })
      await upstream.body?.cancel()
    } catch {
      // The access token still expires on its own within 15 minutes.
    }
  }

  const response = new NextResponse(null, { status: 204 })
  clearSessionCookies(response.cookies)
  return response
}

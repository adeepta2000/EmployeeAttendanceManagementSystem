import { NextResponse, type NextRequest } from "next/server"

import { CookieNames, writeSessionCookies, type TokenPair } from "@/lib/bff/cookies"
import { isSameOriginRequest } from "@/lib/bff/csrf"
import { BffErrors } from "@/lib/bff/problem"
import { isMissingEndpoint, notAvailableYet } from "@/lib/bff/relay"
import { parseBody } from "@/lib/bff/validate"
import { callWithSession } from "@/lib/bff/with-session"
import { changePasswordSchema } from "@/lib/schemas/auth"

/**
 * Changes the signed-in user's password (screen 4, forced on first login). Expected contract:
 * `POST /api/auth/change-password { currentPassword, newPassword }` with the bearer token, answering
 * either 200 with a fresh token pair (the old sessions revoked) or 204. A wrong current password must be
 * a 400, not a 401: a 401 means the session is over and signs the user out.
 */
export async function POST(request: NextRequest) {
  if (!isSameOriginRequest(request)) return BffErrors.forbiddenOrigin()

  const input = await parseBody(request, changePasswordSchema)
  if ("error" in input) return input.error

  const upstream = await callWithSession(request, {
    method: "POST",
    path: "/api/auth/change-password",
    body: { currentPassword: input.data.currentPassword, newPassword: input.data.newPassword },
  })

  if (isMissingEndpoint(upstream.status)) return notAvailableYet()
  if (!upstream.ok) return upstream

  const tokens = upstream.status === 200 ? ((await upstream.json()) as Partial<TokenPair>) : null
  const response = new NextResponse(null, { status: 204 })

  // Keep any cookies callWithSession renewed, then let the new session (if any) replace them.
  for (const cookie of upstream.cookies.getAll()) response.cookies.set(cookie)
  if (tokens?.accessToken) writeSessionCookies(response.cookies, tokens as TokenPair)
  response.cookies.delete(CookieNames.mustChangePassword)
  return response
}

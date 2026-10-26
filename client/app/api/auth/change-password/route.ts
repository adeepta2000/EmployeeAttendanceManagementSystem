import { NextResponse, type NextRequest } from "next/server"

import { writeSessionCookies, type TokenPair } from "@/lib/bff/cookies"
import { isSameOriginRequest } from "@/lib/bff/csrf"
import { BffErrors } from "@/lib/bff/problem"
import { parseBody } from "@/lib/bff/validate"
import { callWithSession } from "@/lib/bff/with-session"
import { changePasswordSchema } from "@/lib/schemas/auth"

/**
 * Changes the signed-in user's password (screen 4, forced on first login). The API ends every session of
 * the account and answers with a fresh token pair, which replaces the cookies. A wrong current password
 * is a 400 (`auth.invalid_current_password`), so it never reads as the end of the session.
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

  if (!upstream.ok) return upstream

  // The new pair replaces whatever callWithSession may have renewed on the way, and clears nw_pwd.
  const tokens = (await upstream.json()) as TokenPair
  const response = new NextResponse(null, { status: 204 })
  writeSessionCookies(response.cookies, tokens)
  return response
}

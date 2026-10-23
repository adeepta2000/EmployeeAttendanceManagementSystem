import { NextResponse, type NextRequest } from "next/server"

import { clientInfo } from "@/lib/bff/client-info"
import { writeSessionCookies, type TokenPair } from "@/lib/bff/cookies"
import { isSameOriginRequest } from "@/lib/bff/csrf"
import { BffErrors } from "@/lib/bff/problem"
import { relay } from "@/lib/bff/relay"
import { callUpstream, newCorrelationId, UpstreamUnavailableError } from "@/lib/bff/upstream"
import { parseBody } from "@/lib/bff/validate"
import { loginSchema } from "@/lib/schemas/auth"

/**
 * Signs in (screen 1). The API returns the token pair in its response body; it goes straight into
 * httpOnly cookies and the browser gets back only whether a password change is due.
 */
export async function POST(request: NextRequest) {
  if (!isSameOriginRequest(request)) return BffErrors.forbiddenOrigin()

  const input = await parseBody(request, loginSchema)
  if ("error" in input) return input.error

  let upstream: Response
  try {
    upstream = await callUpstream({
      method: "POST",
      path: "/api/auth/login",
      client: clientInfo(request.headers),
      correlationId: newCorrelationId(),
      body: input.data,
    })
  } catch (error) {
    if (error instanceof UpstreamUnavailableError) return BffErrors.unavailable()
    throw error
  }

  // 401 (the one generic "invalid credentials"), 429 (rate limited) and 400 pass through as they are.
  if (!upstream.ok) return relay(upstream)

  const tokens = (await upstream.json()) as TokenPair
  const response = NextResponse.json(
    { mustChangePassword: tokens.mustChangePassword },
    { headers: { "Cache-Control": "no-store" } },
  )
  writeSessionCookies(response.cookies, tokens)
  return response
}

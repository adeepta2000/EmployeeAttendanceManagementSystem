import { NextResponse, type NextRequest } from "next/server"

import { clientInfo } from "@/lib/bff/client-info"
import { isSameOriginRequest } from "@/lib/bff/csrf"
import { BffErrors } from "@/lib/bff/problem"
import { relay } from "@/lib/bff/relay"
import { callUpstream, newCorrelationId, UpstreamUnavailableError } from "@/lib/bff/upstream"
import { parseBody } from "@/lib/bff/validate"
import { resetPasswordSchema } from "@/lib/schemas/auth"

/**
 * Sets a new password with the one-time token from a reset link (screen 3). The API answers 204, or 400
 * `auth.invalid_reset_token` when the link is unknown, used, replaced or expired. Every session of the
 * account ends; the user signs in afresh afterwards, so no session is started here.
 */
export async function POST(request: NextRequest) {
  if (!isSameOriginRequest(request)) return BffErrors.forbiddenOrigin()

  const input = await parseBody(request, resetPasswordSchema)
  if ("error" in input) return input.error

  let upstream: Response
  try {
    upstream = await callUpstream({
      method: "POST",
      path: "/api/auth/reset-password",
      client: clientInfo(request.headers),
      correlationId: newCorrelationId(),
      body: { token: input.data.token, newPassword: input.data.newPassword },
    })
  } catch (error) {
    if (error instanceof UpstreamUnavailableError) return BffErrors.unavailable()
    throw error
  }

  if (!upstream.ok) return relay(upstream)

  await upstream.body?.cancel()
  return new NextResponse(null, { status: 204 })
}

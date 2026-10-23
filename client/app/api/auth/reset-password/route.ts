import { NextResponse, type NextRequest } from "next/server"

import { clientInfo } from "@/lib/bff/client-info"
import { isSameOriginRequest } from "@/lib/bff/csrf"
import { BffErrors } from "@/lib/bff/problem"
import { isMissingEndpoint, notAvailableYet, relay } from "@/lib/bff/relay"
import { callUpstream, newCorrelationId, UpstreamUnavailableError } from "@/lib/bff/upstream"
import { parseBody } from "@/lib/bff/validate"
import { resetPasswordSchema } from "@/lib/schemas/auth"

/**
 * Sets a new password with the one-time token from a reset link (screen 3). Expected contract:
 * `POST /api/auth/reset-password { token, newPassword }` answers 204, or 400 when the token is unknown,
 * used or expired. The user signs in afresh afterwards; no session is started here.
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

  if (isMissingEndpoint(upstream.status, { anonymous: true })) return notAvailableYet()
  if (!upstream.ok) return relay(upstream)

  await upstream.body?.cancel()
  return new NextResponse(null, { status: 204 })
}

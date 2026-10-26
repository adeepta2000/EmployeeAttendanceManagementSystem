import { NextResponse, type NextRequest } from "next/server"

import { clientInfo } from "@/lib/bff/client-info"
import { isSameOriginRequest } from "@/lib/bff/csrf"
import { BffErrors } from "@/lib/bff/problem"
import { relay } from "@/lib/bff/relay"
import { callUpstream, newCorrelationId, UpstreamUnavailableError } from "@/lib/bff/upstream"
import { parseBody } from "@/lib/bff/validate"
import { forgotPasswordSchema } from "@/lib/schemas/auth"

/**
 * Asks the API to email a reset link (screen 2). The API answers 202 whether or not the address has an
 * account, before doing any work, so neither the answer nor its timing reveals anything (§2.6).
 */
export async function POST(request: NextRequest) {
  if (!isSameOriginRequest(request)) return BffErrors.forbiddenOrigin()

  const input = await parseBody(request, forgotPasswordSchema)
  if ("error" in input) return input.error

  let upstream: Response
  try {
    upstream = await callUpstream({
      method: "POST",
      path: "/api/auth/forgot-password",
      client: clientInfo(request.headers),
      correlationId: newCorrelationId(),
      body: input.data,
    })
  } catch (error) {
    if (error instanceof UpstreamUnavailableError) return BffErrors.unavailable()
    throw error
  }

  if (!upstream.ok) return relay(upstream)

  await upstream.body?.cancel()
  return new NextResponse(null, { status: 202 })
}

import "server-only"

import { NextResponse, type NextRequest } from "next/server"

import { clientInfo } from "./client-info"
import { bffConfig } from "./config"
import { clearSessionCookies, CookieNames, writeSessionCookies, type TokenPair } from "./cookies"
import { BffErrors } from "./problem"
import { refreshSession } from "./refresh"
import { relay } from "./relay"
import { isExpiring, readSession } from "./session"
import { callUpstream, newCorrelationId, UpstreamUnavailableError, type UpstreamRequest } from "./upstream"

type Call = Pick<UpstreamRequest, "method" | "path" | "body">

/**
 * Calls the API on behalf of the signed-in user (spec §2.5). The access token comes from its httpOnly
 * cookie and goes into the Authorization header here, on the server, so browser JavaScript never sees it.
 *
 * - An access token that is missing or about to expire is refreshed before the call.
 * - A 401 from the API (the token was revoked or the account deactivated) triggers one refresh and one
 *   retry, so an expired token never surfaces to the user as an error.
 * - When the session can't be renewed, the cookies are cleared and the client gets a 401 telling it to
 *   sign in again.
 */
export async function callWithSession(request: NextRequest, call: Call): Promise<NextResponse> {
  const client = clientInfo(request.headers)
  const correlationId = newCorrelationId()
  const refreshToken = request.cookies.get(CookieNames.refreshToken)?.value

  let accessToken = request.cookies.get(CookieNames.accessToken)?.value
  const session = readSession(accessToken)
  if (!session || isExpiring(session, bffConfig.accessTokenSkewSeconds)) accessToken = undefined

  let renewed: TokenPair | undefined

  const renew = async (): Promise<"ok" | NextResponse> => {
    if (!refreshToken) return signedOut()
    const outcome = await refreshSession(refreshToken, client)
    if (outcome.kind === "rejected") return signedOut()
    if (outcome.kind === "unavailable") return BffErrors.unavailable()
    renewed = outcome.tokens
    accessToken = outcome.tokens.accessToken
    return "ok"
  }

  try {
    if (!accessToken) {
      const result = await renew()
      if (result !== "ok") return result
    }

    const send = () => callUpstream({ ...call, client, accessToken, correlationId, signal: request.signal })
    let upstream = await send()

    if (upstream.status === 401 && !renewed) {
      await upstream.body?.cancel()
      const result = await renew()
      if (result !== "ok") return result
      upstream = await send()
    }

    if (upstream.status === 401) {
      await upstream.body?.cancel()
      return signedOut()
    }

    const response = relay(upstream)
    if (renewed) writeSessionCookies(response.cookies, renewed)
    return response
  } catch (error) {
    if (error instanceof UpstreamUnavailableError) return BffErrors.unavailable()
    // The browser gave up (navigated away, or TanStack Query cancelled a superseded fetch): nobody is left
    // to read an answer, so end quietly instead of logging an error.
    if (request.signal.aborted) return new NextResponse(null, { status: 499 })
    throw error
  }
}

function signedOut(): NextResponse {
  const response = BffErrors.sessionExpired()
  clearSessionCookies(response.cookies)
  return response
}

import type { ClientInfo } from "./client-info"
import type { TokenPair } from "./cookies"
import { callUpstream, UpstreamUnavailableError } from "./upstream"

export type RefreshOutcome =
  | { kind: "refreshed"; tokens: TokenPair }
  /** The API rejected the refresh token: the session is over and the cookies must go. */
  | { kind: "rejected" }
  /** The API couldn't be reached. The session may still be fine; keep the cookies and let the user retry. */
  | { kind: "unavailable" }

type Flight = { promise: Promise<RefreshOutcome>; settledAt?: number }

/**
 * How long a finished refresh is remembered. Requests the browser sent with the old cookie before the
 * new one arrived get the same new tokens instead of presenting a revoked token (see below).
 */
const GRACE_MS = 20_000

// Shared by every module instance in this server process (proxy.ts and Route Handlers are bundled
// separately, so a plain module-level Map would not be). With several BFF instances, route one session to
// one instance (sticky sessions) or move this into a shared store.
const flights: Map<string, Flight> = ((globalThis as Record<symbol, unknown>)[Symbol.for("nordwin.bff.refreshFlights")] ??=
  new Map<string, Flight>()) as Map<string, Flight>

/**
 * Exchanges a refresh token for a new pair, at most once per token ("Reuse detection").
 *
 * The API revokes the whole token family when a revoked refresh token is presented again. A page that
 * fires several requests just as the access token expires would otherwise present the same refresh token
 * several times and sign its own user out. So concurrent callers share one upstream call, and callers that
 * arrive shortly after reuse its result.
 *
 * The key includes the user agent: a stolen token replayed from another client doesn't share the flight,
 * reaches the API, and trips reuse detection as it should.
 */
export async function refreshSession(refreshToken: string, client: ClientInfo): Promise<RefreshOutcome> {
  sweep()
  const key = await flightKey(refreshToken, client.userAgent)

  const existing = flights.get(key)
  if (existing) return existing.promise

  const flight: Flight = { promise: exchange(refreshToken, client) }
  flights.set(key, flight)
  flight.promise.then(
    (outcome) => {
      // An outage isn't remembered, so the next request tries again.
      if (outcome.kind === "unavailable") flights.delete(key)
      else flight.settledAt = Date.now()
    },
    () => flights.delete(key),
  )
  return flight.promise
}

async function exchange(refreshToken: string, client: ClientInfo): Promise<RefreshOutcome> {
  try {
    const response = await callUpstream({
      method: "POST",
      path: "/api/auth/refresh",
      client,
      body: { refreshToken },
    })
    if (response.ok) return { kind: "refreshed", tokens: (await response.json()) as TokenPair }
    if (response.status === 401 || response.status === 400) return { kind: "rejected" }
    return { kind: "unavailable" }
  } catch (error) {
    // Unreachable, timed out, or a 200 whose body wasn't the expected JSON.
    if (error instanceof UpstreamUnavailableError || error instanceof SyntaxError) return { kind: "unavailable" }
    throw error
  }
}

function sweep() {
  const cutoff = Date.now() - GRACE_MS
  for (const [key, flight] of flights) {
    if (flight.settledAt !== undefined && flight.settledAt < cutoff) flights.delete(key)
  }
}

// The map holds hashes, never the tokens themselves.
async function flightKey(refreshToken: string, userAgent: string | null): Promise<string> {
  const data = new TextEncoder().encode(`${refreshToken}\n${userAgent ?? ""}`)
  const digest = await crypto.subtle.digest("SHA-256", data)
  return Buffer.from(digest).toString("base64url")
}

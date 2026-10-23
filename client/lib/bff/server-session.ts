import "server-only"

import { cookies, headers } from "next/headers"

import { clientInfo } from "./client-info"
import { CookieNames } from "./cookies"
import { readSession, type Session } from "./session"
import { callUpstream, newCorrelationId, UpstreamUnavailableError } from "./upstream"

// Session access for Server Components. proxy.ts has already renewed an expired token before the render
// starts, so these read the cookie as it is and never refresh.

export async function getSession(): Promise<Session | null> {
  return readSession((await cookies()).get(CookieNames.accessToken)?.value)
}

/** GET from the API as the signed-in user; null when there is no session or the API says no. */
export async function serverGet<T>(path: string): Promise<T | null> {
  const accessToken = (await cookies()).get(CookieNames.accessToken)?.value
  if (!accessToken) return null
  try {
    const response = await callUpstream({
      method: "GET",
      path,
      accessToken,
      client: clientInfo(await headers()),
      correlationId: newCorrelationId(),
    })
    if (!response.ok) {
      await response.body?.cancel()
      return null
    }
    return (await response.json()) as T
  } catch (error) {
    if (error instanceof UpstreamUnavailableError) return null
    throw error
  }
}

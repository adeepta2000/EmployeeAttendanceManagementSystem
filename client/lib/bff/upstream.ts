import { bffConfig } from "./config"
import type { ClientInfo } from "./client-info"

export type UpstreamRequest = {
  method: string
  /** Path under the API root, e.g. `/api/employees?page=2`. */
  path: string
  client: ClientInfo
  accessToken?: string
  /** Serialized to JSON. */
  body?: unknown
  correlationId?: string
  signal?: AbortSignal
}

/** The API couldn't be reached or didn't answer in time. Distinct from an HTTP error response. */
export class UpstreamUnavailableError extends Error {
  constructor(cause: unknown) {
    super("The attendance API could not be reached.", { cause })
    this.name = "UpstreamUnavailableError"
  }
}

/**
 * One call from the BFF to the .NET API. The browser's cookies are never forwarded: the API sees only
 * the bearer token, the client's IP and user agent (for the login rate limit and session metadata) and a
 * correlation ID that ties the BFF's and the API's logs together.
 */
export async function callUpstream(request: UpstreamRequest): Promise<Response> {
  const headers = new Headers({ Accept: "application/json, application/problem+json" })
  headers.set("X-Forwarded-For", request.client.ip)
  if (request.client.userAgent) headers.set("User-Agent", request.client.userAgent)
  if (request.accessToken) headers.set("Authorization", `Bearer ${request.accessToken}`)
  if (request.correlationId) headers.set("X-Correlation-ID", request.correlationId)
  if (request.body !== undefined) headers.set("Content-Type", "application/json")

  const timeout = AbortSignal.timeout(bffConfig.upstreamTimeoutMs)
  try {
    return await fetch(`${bffConfig.apiBaseUrl}${request.path}`, {
      method: request.method,
      headers,
      body: request.body === undefined ? undefined : JSON.stringify(request.body),
      cache: "no-store",
      redirect: "manual",
      signal: request.signal ? AbortSignal.any([request.signal, timeout]) : timeout,
    })
  } catch (error) {
    // A caller that went away is not an outage; let the abort propagate as-is.
    if (request.signal?.aborted) throw error
    throw new UpstreamUnavailableError(error)
  }
}

/** A short ID the API accepts as X-Correlation-ID (letters, digits, `-`, at most 64 characters). */
export function newCorrelationId(): string {
  return crypto.randomUUID().replaceAll("-", "")
}

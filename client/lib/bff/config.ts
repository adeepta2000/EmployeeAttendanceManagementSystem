// Server-side settings of the BFF (spec §2.5). Imported by proxy.ts and Route Handlers only; nothing
// here reaches the browser bundle, because none of it is NEXT_PUBLIC_.

function readApiBaseUrl(): string {
  const value = process.env.API_BASE_URL
  if (!value) {
    throw new Error("API_BASE_URL is not set. Copy client/.env.example to client/.env.local and point it at the API.")
  }
  return value.replace(/\/+$/, "")
}

export const bffConfig = {
  /** The .NET API. Only the server talks to it; the browser only ever calls this app's /api routes. */
  get apiBaseUrl() {
    return readApiBaseUrl()
  },

  /**
   * How many reverse proxies in front of Next.js append to X-Forwarded-For. The client IP passed on to
   * the API (login rate limit, session metadata) is the entry that many places from the right; with 0,
   * Next.js is exposed directly and the right-most entry is used. A client can prepend fake entries, but
   * never forge the ones a trusted proxy appends.
   */
  trustedProxyHops: Math.max(0, Number.parseInt(process.env.TRUSTED_PROXY_HOPS ?? "0", 10) || 0),

  /** Upstream calls that take longer than this fail with "can't reach the server". */
  upstreamTimeoutMs: 15_000,

  /** An access token this close to expiry is refreshed before use, so it can't expire in flight. */
  accessTokenSkewSeconds: 30,
} as const

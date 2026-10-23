import { bffConfig } from "./config"

/** Who is calling, as the API needs it for rate limiting and session metadata (§2.5). */
export type ClientInfo = { ip: string; userAgent: string | null }

const MAX_USER_AGENT = 400 // RefreshTokens.UserAgent is NVARCHAR(400)

export function clientInfo(headers: Headers): ClientInfo {
  // Next.js fills X-Forwarded-For from the socket only when the header is missing, so its left-hand
  // entries may be the client's own invention. Count from the right, past the proxies we trust.
  const forwarded = (headers.get("x-forwarded-for") ?? "")
    .split(",")
    .map((entry) => entry.trim())
    .filter(Boolean)
  const index = forwarded.length - 1 - bffConfig.trustedProxyHops
  const ip = forwarded[Math.max(0, index)] ?? "unknown"

  const userAgent = headers.get("user-agent")
  return { ip: ip.slice(0, 45), userAgent: userAgent ? userAgent.slice(0, MAX_USER_AGENT) : null }
}

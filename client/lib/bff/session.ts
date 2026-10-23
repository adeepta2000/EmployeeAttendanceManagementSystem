// Reads the claims of the API's access token (spec §2.5): `sub` (user PublicId), `employee_id`, one
// `permission` claim per code, and `exp`.
//
// The signature is not checked here, deliberately: the BFF never holds the signing key, and the claims
// only decide what to render and where to redirect. Every data request carries the token to the API,
// which verifies it, so a forged cookie gets an empty page and a 401, never data.

export type Session = {
  userId: string
  /** Absent for an account with no employee record (the IT admin). */
  employeeId: string | null
  permissions: string[]
  /** Unix seconds. */
  expiresAt: number
}

type AccessTokenClaims = {
  sub?: unknown
  employee_id?: unknown
  permission?: unknown
  exp?: unknown
}

function decodePayload(token: string): AccessTokenClaims | null {
  const parts = token.split(".")
  if (parts.length !== 3) return null
  try {
    const json = Buffer.from(parts[1], "base64url").toString("utf8")
    const payload: unknown = JSON.parse(json)
    return typeof payload === "object" && payload !== null ? (payload as AccessTokenClaims) : null
  } catch {
    return null
  }
}

/** The session in an access token, or null if the token is malformed. Expiry is not checked. */
export function readSession(accessToken: string | undefined): Session | null {
  if (!accessToken) return null
  const claims = decodePayload(accessToken)
  if (!claims || typeof claims.sub !== "string" || typeof claims.exp !== "number") return null

  // A JWT carries a repeated claim as an array, and a single one as a plain string.
  const permissions = Array.isArray(claims.permission)
    ? claims.permission.filter((p): p is string => typeof p === "string")
    : typeof claims.permission === "string"
      ? [claims.permission]
      : []

  return {
    userId: claims.sub,
    employeeId: typeof claims.employee_id === "string" ? claims.employee_id : null,
    permissions,
    expiresAt: claims.exp,
  }
}

/** True when the session expires within `skewSeconds` from now (or already has). */
export function isExpiring(session: Session, skewSeconds: number): boolean {
  return session.expiresAt - skewSeconds <= Date.now() / 1000
}

/**
 * The `next` parameter after sign-in, accepted only as a path on this site. Anything else (another
 * origin, `//evil.example`, `/\evil.example`, an API route) falls back to the home page, so the login
 * link can't be turned into an open redirect.
 */
export function safeNextPath(next: string | null | undefined): string {
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.startsWith("/\\")) return "/"
  if (next.startsWith("/api/") || next === "/login") return "/"
  return next
}

// Cookie names, shared by the server (lib/bff) and the client code that reads the readable ones.

// __Host- binds a cookie to this exact origin: it can't be set by a sibling subdomain or with a Domain.
// Browsers only honour it over HTTPS, so development on http://localhost uses the plain names.
const prefix = process.env.NODE_ENV === "production" ? "__Host-" : ""

/** Session cookies, set only by Route Handlers and proxy.ts. All httpOnly except `csrf`. */
export const CookieNames = {
  accessToken: `${prefix}nw_at`,
  refreshToken: `${prefix}nw_rt`,
  /** Set while the account must change its password before doing anything else (screen 4). */
  mustChangePassword: `${prefix}nw_pwd`,
  /** Double-submit anti-forgery token. Readable by page JavaScript on purpose; it is not a credential. */
  csrf: `${prefix}nw_csrf`,
} as const

/** UI preferences: not secrets, written by client code, read on the server to render without a flash. */
export const PreferenceCookies = {
  sidebar: "nw_sidebar",
  density: "nw_density",
} as const

/** Reads a cookie that page JavaScript is allowed to see. */
export function readClientCookie(name: string): string | undefined {
  if (typeof document === "undefined") return undefined
  const entry = document.cookie.split("; ").find((pair) => pair.startsWith(`${name}=`))
  return entry ? decodeURIComponent(entry.slice(name.length + 1)) : undefined
}

/** Writes a one-year UI preference cookie. */
export function writePreferenceCookie(name: string, value: string) {
  document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=31536000; samesite=lax${
    location.protocol === "https:" ? "; secure" : ""
  }`
}

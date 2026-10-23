import { NextResponse, type NextRequest } from "next/server"

import { clientInfo } from "@/lib/bff/client-info"
import { bffConfig } from "@/lib/bff/config"
import {
  clearSessionCookies,
  CookieNames,
  writeCsrfCookie,
  writeSessionCookies,
  type TokenPair,
} from "@/lib/bff/cookies"
import { newCsrfToken } from "@/lib/bff/csrf"
import { refreshSession } from "@/lib/bff/refresh"
import { isExpiring, readSession, type Session } from "@/lib/bff/session"

// Next.js 16 renamed middleware to proxy. This one runs before every page render and:
//
// 1. Keeps signed-out users out of the app: every page except the public auth pages needs a session.
// 2. Renews an expired access token before the page renders (Server Components can't set cookies), and
//    hands the new token to the render by rewriting the request's Cookie header.
// 3. Holds a user who must change their password on /change-password (screen 4).
// 4. Issues the anti-forgery cookie (lib/bff/csrf.ts) and a per-request CSP nonce (§2.6).
//
// Route Handlers under /api are excluded: they check the session themselves (lib/bff/with-session.ts).

const PUBLIC_PAGES = new Set(["/login", "/forgot-password", "/reset-password"])
const CHANGE_PASSWORD_PAGE = "/change-password"

export async function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl
  const isPublic = PUBLIC_PAGES.has(pathname)

  const refreshToken = request.cookies.get(CookieNames.refreshToken)?.value
  let session: Session | null = readSession(request.cookies.get(CookieNames.accessToken)?.value)
  if (session && isExpiring(session, bffConfig.accessTokenSkewSeconds)) session = null

  let renewed: TokenPair | undefined
  let rejected = false
  let unavailable = false
  if (!session && refreshToken) {
    const outcome = await refreshSession(refreshToken, clientInfo(request.headers))
    if (outcome.kind === "refreshed") {
      renewed = outcome.tokens
      session = readSession(renewed.accessToken)
    } else if (outcome.kind === "rejected") {
      rejected = true
    } else {
      unavailable = true
    }
  }

  const mustChangePassword = renewed
    ? renewed.mustChangePassword
    : request.cookies.has(CookieNames.mustChangePassword)

  let response: NextResponse
  if (!session && !isPublic) {
    const login = new URL("/login", request.url)
    if (pathname !== "/") login.searchParams.set("next", `${pathname}${search}`)
    if (unavailable) login.searchParams.set("reason", "unavailable")
    else if (refreshToken) login.searchParams.set("reason", "expired")
    response = NextResponse.redirect(login)
  } else if (session && mustChangePassword && pathname !== CHANGE_PASSWORD_PAGE && pathname !== "/login") {
    response = NextResponse.redirect(new URL(CHANGE_PASSWORD_PAGE, request.url))
  } else if (session && pathname === "/login") {
    response = NextResponse.redirect(new URL("/", request.url))
  } else {
    response = render(request, renewed, rejected)
  }

  if (renewed) writeSessionCookies(response.cookies, renewed)
  if (rejected) clearSessionCookies(response.cookies)
  if (!request.cookies.has(CookieNames.csrf)) writeCsrfCookie(response.cookies, newCsrfToken())
  return response
}

function render(request: NextRequest, renewed: TokenPair | undefined, rejected: boolean): NextResponse {
  const nonce = Buffer.from(crypto.getRandomValues(new Uint8Array(16))).toString("base64")
  const csp = contentSecurityPolicy(nonce)

  const headers = new Headers(request.headers)
  // Next.js reads the nonce from this request header and puts it on its own <script> tags.
  headers.set("content-security-policy", csp)
  headers.set("x-nonce", nonce)

  if (renewed || rejected) {
    const jar = new Map(request.cookies.getAll().map((cookie) => [cookie.name, cookie.value]))
    if (renewed) {
      jar.set(CookieNames.accessToken, renewed.accessToken)
      jar.set(CookieNames.refreshToken, renewed.refreshToken)
      if (renewed.mustChangePassword) jar.set(CookieNames.mustChangePassword, "1")
      else jar.delete(CookieNames.mustChangePassword)
    } else {
      jar.delete(CookieNames.accessToken)
      jar.delete(CookieNames.refreshToken)
      jar.delete(CookieNames.mustChangePassword)
    }
    headers.set(
      "cookie",
      [...jar].map(([name, value]) => `${name}=${encodeURIComponent(value)}`).join("; "),
    )
  }

  const response = NextResponse.next({ request: { headers } })
  response.headers.set("Content-Security-Policy", csp)
  return response
}

/**
 * Scripts run only with this request's nonce ('strict-dynamic' lets those load their own chunks).
 * Styles allow inline: Radix positions popovers with style attributes, and injected CSS can't run code.
 */
function contentSecurityPolicy(nonce: string): string {
  const dev = process.env.NODE_ENV === "development"
  return [
    "default-src 'self'",
    // React uses eval in development only, to rebuild server error stacks in the browser.
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${dev ? " 'unsafe-eval'" : ""}`,
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' blob: data:",
    "font-src 'self'",
    "connect-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
    ...(dev ? [] : ["upgrade-insecure-requests"]),
  ].join("; ")
}

export const config = {
  // Everything except Route Handlers, build assets and files in public/.
  matcher: ["/((?!api/|_next/static|_next/image|favicon.ico|.*\\.[a-zA-Z0-9]+$).*)"],
}

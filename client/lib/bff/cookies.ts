// The session cookies (spec §2.5). Both tokens live in httpOnly cookies that browser JavaScript can't
// read; the BFF moves them between these cookies and the API's Authorization header.

import type { ResponseCookie } from "next/dist/compiled/@edge-runtime/cookies"

import { CookieNames } from "@/lib/cookie-names"

export { CookieNames, PreferenceCookies } from "@/lib/cookie-names"

const sessionCookie = {
  httpOnly: true,
  secure: true,
  sameSite: "lax",
  path: "/",
} satisfies Partial<ResponseCookie>

export type TokenPair = {
  accessToken: string
  accessTokenExpiresUtc: string
  refreshToken: string
  refreshTokenExpiresUtc: string
  mustChangePassword: boolean
}

/** Anything with Next's cookie-store `set`/`delete` (a NextResponse, or `cookies()` in a Route Handler). */
type CookieWriter = {
  set(cookie: ResponseCookie): unknown
  delete(name: string): unknown
}

export function writeSessionCookies(cookies: CookieWriter, tokens: TokenPair) {
  cookies.set({
    ...sessionCookie,
    name: CookieNames.accessToken,
    value: tokens.accessToken,
    expires: new Date(tokens.accessTokenExpiresUtc),
  })
  cookies.set({
    ...sessionCookie,
    name: CookieNames.refreshToken,
    value: tokens.refreshToken,
    expires: new Date(tokens.refreshTokenExpiresUtc),
  })

  if (tokens.mustChangePassword) {
    cookies.set({
      ...sessionCookie,
      name: CookieNames.mustChangePassword,
      value: "1",
      expires: new Date(tokens.refreshTokenExpiresUtc),
    })
  } else {
    cookies.delete(CookieNames.mustChangePassword)
  }
}

export function clearSessionCookies(cookies: CookieWriter) {
  cookies.delete(CookieNames.accessToken)
  cookies.delete(CookieNames.refreshToken)
  cookies.delete(CookieNames.mustChangePassword)
}

export function writeCsrfCookie(cookies: CookieWriter, token: string) {
  cookies.set({ name: CookieNames.csrf, value: token, secure: true, sameSite: "strict", path: "/" })
}

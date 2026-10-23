import { NextResponse } from "next/server"

import { problem } from "./problem"

/** Headers of an API response that the browser may see. Nothing else is passed through. */
const PASSTHROUGH_HEADERS = ["content-type", "content-disposition", "x-correlation-id", "retry-after"]

/** Passes an API response on to the browser: status, body and a short list of safe headers. */
export function relay(upstream: Response): NextResponse {
  const headers = new Headers({ "Cache-Control": "no-store" })
  for (const name of PASSTHROUGH_HEADERS) {
    const value = upstream.headers.get(name)
    if (value) headers.set(name, value)
  }
  const body = upstream.status === 204 || upstream.status === 304 ? null : upstream.body
  return new NextResponse(body, { status: upstream.status, headers })
}

/**
 * For an anonymous call, a missing route answers 401, not 404: the API's fallback policy requires a
 * signed-in user wherever no endpoint opted out. The real forgot/reset endpoints are anonymous and never
 * answer 401, so for them a 401 means the same thing.
 */
export function isMissingEndpoint(status: number, { anonymous = false } = {}): boolean {
  return status === 404 || status === 405 || (anonymous && status === 401)
}

export function notAvailableYet(): NextResponse {
  return problem(
    501,
    "auth.not_available",
    "This isn't switched on yet. Ask HR or IT to reset your password for you.",
  )
}

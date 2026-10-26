import { NextResponse } from "next/server"

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

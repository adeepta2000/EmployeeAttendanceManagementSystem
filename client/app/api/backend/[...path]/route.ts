import type { NextRequest } from "next/server"

import { issuesByField, matchBackendRoute, toQueryString } from "@/lib/bff/backend-routes"
import { CookieNames } from "@/lib/bff/cookies"
import { isSameOriginRequest } from "@/lib/bff/csrf"
import { BffErrors, validationProblem } from "@/lib/bff/problem"
import { callWithSession } from "@/lib/bff/with-session"

// The BFF's proxy to the API for data calls (spec §2.5). The browser calls /api/backend/employees?…; this
// handler checks the route is on the allowlist, validates the input with the same Zod schema the form
// used, attaches the access token on the server and relays the API's answer.

type Context = RouteContext<"/api/backend/[...path]">

async function handle(request: NextRequest, context: Context): Promise<Response> {
  const { path } = await context.params
  const route = matchBackendRoute(request.method, path)
  if (!route) return BffErrors.notFound()

  if (request.method !== "GET" && !isSameOriginRequest(request)) return BffErrors.forbiddenOrigin()

  // Screen 4: until a forced password change is done, the account can do nothing else. The API doesn't
 
  if (request.cookies.has(CookieNames.mustChangePassword)) return BffErrors.passwordChangeRequired()

  let query = ""
  if (route.query) {
    const parsed = route.query.safeParse(Object.fromEntries(request.nextUrl.searchParams))
    if (!parsed.success) return validationProblem(issuesByField(parsed.error))
    query = toQueryString(parsed.data)
  }

  let body: unknown
  if (route.body) {
    let json: unknown
    try {
      json = await request.json()
    } catch {
      return BffErrors.invalidJson()
    }
    const parsed = route.body.safeParse(json)
    if (!parsed.success) return validationProblem(issuesByField(parsed.error))
    body = parsed.data
  }

  return callWithSession(request, {
    method: request.method,
    path: `/api/${path.map(encodeURIComponent).join("/")}${query}`,
    body,
  })
}

export const GET = handle
export const POST = handle
export const PUT = handle

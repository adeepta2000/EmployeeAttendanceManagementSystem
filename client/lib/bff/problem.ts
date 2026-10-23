import { NextResponse } from "next/server"

/**
 * Errors raised by the BFF itself, in the same RFC 9457 shape as the API's (`detail` plus a `code`
 * extension, and `errors` for validation), so the client handles both alike.
 */
export function problem(
  status: number,
  code: string,
  detail: string,
  extensions: Record<string, unknown> = {},
): NextResponse {
  return NextResponse.json(
    { status, title: titles[status] ?? "Error", detail, code, ...extensions },
    { status, headers: { "Content-Type": "application/problem+json", "Cache-Control": "no-store" } },
  )
}

/** A 400 listing each invalid field, keyed by its JSON name, like the API's HttpValidationProblemDetails. */
export function validationProblem(errors: Record<string, string[]>): NextResponse {
  return problem(400, "validation", "One or more fields are invalid.", { errors })
}

export const BffErrors = {
  sessionExpired: () => problem(401, "session.expired", "Your session has ended. Sign in again."),
  forbiddenOrigin: () =>
    problem(403, "csrf.failed", "This request didn't come from the attendance app. Reload the page and try again."),
  unavailable: () =>
    problem(503, "upstream.unavailable", "The attendance server isn't responding. Wait a moment, then try again."),
  passwordChangeRequired: () =>
    problem(403, "auth.password_change_required", "Change your password before doing anything else."),
  notFound: () => problem(404, "route.not_found", "There is no such API route."),
  invalidJson: () => problem(400, "request.invalid_json", "The request body isn't valid JSON."),
}

const titles: Record<number, string> = {
  400: "Bad Request",
  401: "Unauthorized",
  403: "Forbidden",
  404: "Not Found",
  409: "Conflict",
  501: "Not Implemented",
  503: "Service Unavailable",
}

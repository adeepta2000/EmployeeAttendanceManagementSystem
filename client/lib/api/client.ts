// The browser's only way to the API: same-origin calls to this app's Route Handlers (/api/auth/*,
// /api/backend/*). The session cookies ride along automatically and are invisible to this code.

import { CookieNames, readClientCookie } from "@/lib/cookie-names"

/** A failed call, in the RFC 9457 shape the API and the BFF both answer with. */
export class ApiError extends Error {
  readonly status: number
  /** The API's or BFF's error code (`employee.code_taken`), when it sent one. */
  readonly code: string | undefined
  /** The server's explanation, already phrased for the user; absent for network failures. */
  readonly detail: string | undefined
  /** Field name (as in the request JSON) to messages. */
  readonly fieldErrors: Record<string, string[]>
  readonly correlationId: string | undefined

  constructor(init: {
    status: number
    code?: string
    detail?: string
    fieldErrors?: Record<string, string[]>
    correlationId?: string
  }) {
    super(init.detail ?? init.code ?? `HTTP ${init.status}`)
    this.name = "ApiError"
    this.status = init.status
    this.code = init.code
    this.detail = init.detail
    this.fieldErrors = init.fieldErrors ?? {}
    this.correlationId = init.correlationId
  }

  /** The request never got an answer (offline, DNS, the dev server stopped). */
  get isNetworkError() {
    return this.status === 0
  }
}

type RequestOptions = {
  method?: "GET" | "POST" | "PUT"
  body?: unknown
  signal?: AbortSignal
}

let redirectingToLogin = false

export async function request<T>(url: string, options: RequestOptions = {}): Promise<T> {
  const method = options.method ?? "GET"
  const headers: Record<string, string> = { Accept: "application/json" }
  if (options.body !== undefined) headers["Content-Type"] = "application/json"
  if (method !== "GET") {
    const csrf = readClientCookie(CookieNames.csrf)
    if (csrf) headers["X-CSRF-Token"] = csrf
  }

  let response: Response
  try {
    response = await fetch(url, {
      method,
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
      credentials: "same-origin",
      cache: "no-store",
    })
  } catch (error) {
    if (options.signal?.aborted) throw error
    throw new ApiError({ status: 0, code: "network" })
  }

  if (!response.ok) {
    const error = await toApiError(response)
    // The BFF answers 401 only once it has failed to renew the session and cleared the cookies.
    if (error.status === 401 && url.startsWith("/api/backend/")) sendToLogin()
    throw error
  }

  if (response.status === 202 || response.status === 204) return undefined as T
  return (await response.json()) as T
}

async function toApiError(response: Response): Promise<ApiError> {
  let body: Record<string, unknown> = {}
  try {
    body = (await response.json()) as Record<string, unknown>
  } catch {
    // An empty or non-JSON error body: the status says enough.
  }
  const errors = body.errors
  return new ApiError({
    status: response.status,
    code: typeof body.code === "string" ? body.code : undefined,
    detail: typeof body.detail === "string" ? body.detail : undefined,
    fieldErrors: errors && typeof errors === "object" ? (errors as Record<string, string[]>) : undefined,
    correlationId:
      (typeof body.correlationId === "string" ? body.correlationId : undefined) ??
      response.headers.get("x-correlation-id") ??
      undefined,
  })
}

function sendToLogin() {
  if (redirectingToLogin || typeof window === "undefined") return
  redirectingToLogin = true
  const next = `${window.location.pathname}${window.location.search}`
  // A full page load: the expired session's cached data must not outlive it.
  // eslint-disable-next-line @next/next/no-location-assign-relative-destination
  window.location.assign(`/login?reason=expired&next=${encodeURIComponent(next)}`)
}

function withQuery(path: string, query?: Record<string, string | number | boolean | undefined | null>) {
  if (!query) return path
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== "") params.set(key, String(value))
  }
  const qs = params.toString()
  return qs ? `${path}?${qs}` : path
}

/** Calls an API route through the BFF proxy: `backend.get("employees", { page: 2 })`. */
export const backend = {
  get: <T>(path: string, query?: Record<string, string | number | boolean | undefined | null>, signal?: AbortSignal) =>
    request<T>(withQuery(`/api/backend/${path}`, query), { signal }),
  post: <T>(path: string, body?: unknown) => request<T>(`/api/backend/${path}`, { method: "POST", body }),
  put: <T>(path: string, body: unknown) => request<T>(`/api/backend/${path}`, { method: "PUT", body }),
}

/** Calls one of the auth Route Handlers: `auth("login", { email, password })`. */
export const auth = <T = void>(action: string, body?: unknown) =>
  request<T>(`/api/auth/${action}`, { method: "POST", body })

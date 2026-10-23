import { z } from "zod"

import {
  activeFilterQuerySchema,
  deactivateEmployeeSchema,
  departmentSchema,
  designationSchema,
  employeeListQuerySchema,
  employeeSchema,
} from "@/lib/schemas/organization"

/**
 * Every API route the browser may reach through /api/backend (app/api/backend/[...path]/route.ts), with
 * the schemas its input must pass first. Anything not listed is a 404 from the BFF, so a new API endpoint
 * is unreachable from the browser until it is added here with its validation.
 *
 * Bodies and query strings are re-serialized from the parsed values, so fields a schema doesn't know
 * never reach the API (mass assignment, §2.6). `:id` segments must be PublicIds (GUIDs).
 */
type BackendRoute = {
  method: "GET" | "POST" | "PUT"
  pattern: string
  query?: z.ZodType<Record<string, unknown>>
  body?: z.ZodType
}

const routes: BackendRoute[] = [
  { method: "GET", pattern: "employees", query: employeeListQuerySchema },
  { method: "POST", pattern: "employees", body: employeeSchema },
  { method: "GET", pattern: "employees/:id" },
  { method: "PUT", pattern: "employees/:id", body: employeeSchema },
  { method: "POST", pattern: "employees/:id/deactivate", body: deactivateEmployeeSchema },
  { method: "POST", pattern: "employees/:id/reactivate" },

  { method: "GET", pattern: "departments", query: activeFilterQuerySchema },
  { method: "POST", pattern: "departments", body: departmentSchema },
  { method: "GET", pattern: "departments/:id" },
  { method: "PUT", pattern: "departments/:id", body: departmentSchema },
  { method: "POST", pattern: "departments/:id/deactivate" },
  { method: "POST", pattern: "departments/:id/activate" },

  { method: "GET", pattern: "designations", query: activeFilterQuerySchema },
  { method: "POST", pattern: "designations", body: designationSchema },
  { method: "GET", pattern: "designations/:id" },
  { method: "PUT", pattern: "designations/:id", body: designationSchema },
  { method: "POST", pattern: "designations/:id/deactivate" },
  { method: "POST", pattern: "designations/:id/activate" },

  // Work locations are read here only to fill the employee form; screen 28 doesn't edit them.
  { method: "GET", pattern: "work-locations", query: activeFilterQuerySchema },
]

const guid = z.guid()

export function matchBackendRoute(method: string, segments: string[]): BackendRoute | null {
  return (
    routes.find((route) => {
      if (route.method !== method) return false
      const parts = route.pattern.split("/")
      return (
        parts.length === segments.length &&
        parts.every((part, i) => (part === ":id" ? guid.safeParse(segments[i]).success : part === segments[i]))
      )
    }) ?? null
  )
}

export function toQueryString(values: Record<string, unknown>): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && value !== null && value !== "") params.set(key, String(value))
  }
  const query = params.toString()
  return query ? `?${query}` : ""
}

/** Zod issues keyed by field, in the shape of the API's validation errors. */
export function issuesByField(error: z.ZodError): Record<string, string[]> {
  const errors: Record<string, string[]> = {}
  for (const issue of error.issues) {
    const field = issue.path.join(".") || "$"
    ;(errors[field] ??= []).push(issue.message)
  }
  return errors
}

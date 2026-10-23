import type { NextRequest, NextResponse } from "next/server"
import type { z } from "zod"

import { issuesByField } from "./backend-routes"
import { BffErrors, validationProblem } from "./problem"

/** Parses a JSON body with the same schema the form used; on failure, the 400 to send back. */
export async function parseBody<T extends z.ZodType>(
  request: NextRequest,
  schema: T,
): Promise<{ data: z.output<T> } | { error: NextResponse }> {
  let json: unknown
  try {
    json = await request.json()
  } catch {
    return { error: BffErrors.invalidJson() }
  }
  const parsed = schema.safeParse(json)
  if (!parsed.success) return { error: validationProblem(issuesByField(parsed.error)) }
  return { data: parsed.data }
}

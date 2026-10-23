import type { FieldPath, FieldValues, UseFormSetError } from "react-hook-form"

import { ApiError } from "@/lib/api/client"

/**
 * Puts the API's (or the BFF's) field errors on the form's fields. `codeFields` maps error codes that
 * belong to one field, such as `employee.code_taken`, onto it. Returns true when at least one error landed
 * on a field; otherwise the caller shows the error above the form.
 */
export function applyApiErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: readonly FieldPath<T>[],
  codeFields: Partial<Record<string, FieldPath<T>>> = {},
): boolean {
  if (!(error instanceof ApiError)) return false
  let applied = false

  for (const [key, messages] of Object.entries(error.fieldErrors)) {
    const field = fields.find((name) => name.toLowerCase() === key.toLowerCase())
    if (field && messages.length > 0) {
      setError(field, { type: "server", message: messages[0] }, { shouldFocus: !applied })
      applied = true
    }
  }

  const codeField = error.code ? codeFields[error.code] : undefined
  if (codeField && error.detail) {
    setError(codeField, { type: "server", message: error.detail }, { shouldFocus: !applied })
    applied = true
  }

  return applied
}

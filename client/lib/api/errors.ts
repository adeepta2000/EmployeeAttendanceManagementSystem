"use client"

import { useTranslations } from "next-intl"
import { useCallback } from "react"

import { parseMessage } from "@/lib/schemas/message"

import { ApiError } from "./client"

/**
 * Turns a failed call into one sentence for the user that says what happened and what to do (spec
 * §4.5). The API's own `detail` texts are written that way already; the generic cases come from the
 * string table.
 */
export function useErrorText() {
  const t = useTranslations("errors")
  return useCallback(
    (error: unknown): string => {
      if (!(error instanceof ApiError)) return t("unexpected")
      if (error.isNetworkError) return t("network")
      if (error.status === 503) return t("unavailable")
      if (error.status === 429) return t("rateLimited")
      if (error.status >= 500) {
        return error.correlationId ? t("serverWithReference", { reference: error.correlationId }) : t("server")
      }
      if (error.status === 403 && !error.code) return t("forbidden")
      if (error.status === 404 && !error.code) return t("notFound")
      return error.detail ?? t("unexpected")
    },
    [t],
  )
}

/** Translates a validation message: a string-table key from a Zod schema, or the API's own sentence. */
export function useMessageText() {
  const t = useTranslations()
  return useCallback(
    (message: string | undefined): string | undefined => {
      if (!message) return undefined
      const parsed = parseMessage(message)
      if (parsed && t.has(parsed.key)) return t(parsed.key, parsed.values)
      return message
    },
    [t],
  )
}

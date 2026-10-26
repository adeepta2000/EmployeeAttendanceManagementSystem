"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import Link from "next/link"
import { useTranslations } from "next-intl"
import { useEffect, useState } from "react"
import { useForm } from "react-hook-form"

import { applyApiErrors } from "@/components/forms/api-errors"
import { TextField } from "@/components/forms/fields"
import { FormAlert } from "@/components/forms/form-alert"
import { Button } from "@/components/ui/button"
import { Spinner } from "@/components/ui/spinner"
import { ApiError, auth } from "@/lib/api/client"
import { useErrorText } from "@/lib/api/errors"
import { PASSWORD_MIN, resetPasswordSchema, type ResetPasswordInput } from "@/lib/schemas/auth"

/** Takes the token out of the URL fragment once, then removes it so it isn't left in history. */
function useResetToken(): string | null | undefined {
  const [token, setToken] = useState<string | null | undefined>(undefined)
  useEffect(() => {
    const fromHash = new URLSearchParams(window.location.hash.slice(1)).get("token")
    // Keep a token already found: a second run (React's StrictMode does one in development, and so does
    // a remount) comes after the fragment has been removed.
    // eslint-disable-next-line react-hooks/set-state-in-effect -- the fragment is only readable after hydration
    setToken((current) => current || fromHash)
    if (fromHash) window.history.replaceState(null, "", window.location.pathname)
  }, [])
  return token
}

export function ResetPasswordForm() {
  const t = useTranslations("auth.reset")
  const errorText = useErrorText()
  const token = useResetToken()
  const [done, setDone] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  // The link itself was refused (used, replaced or expired): the way forward is a new one.
  const [linkRejected, setLinkRejected] = useState(false)

  const form = useForm<ResetPasswordInput>({
    resolver: zodResolver(resetPasswordSchema),
    values: { token: token ?? "", newPassword: "", confirmPassword: "" },
    resetOptions: { keepDirtyValues: true },
  })

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)
    setLinkRejected(false)
    try {
      await auth("reset-password", values)
      setDone(true)
    } catch (error) {
      setLinkRejected(error instanceof ApiError && error.code === "auth.invalid_reset_token")
      if (!applyApiErrors(error, form.setError, ["newPassword", "confirmPassword"])) setFormError(errorText(error))
    }
  })

  if (token === undefined) return null

  if (!token) {
    return (
      <div className="flex flex-col gap-2">
        <h1 className="text-20 font-semibold">{t("title")}</h1>
        <FormAlert>{t("errors.missingToken")}</FormAlert>
        <Link href="/forgot-password" className="self-start text-14 text-brand underline-offset-4 hover:underline">
          {t("requestNew")}
        </Link>
      </div>
    )
  }

  if (done) {
    return (
      <div className="flex flex-col gap-2" role="status">
        <h1 className="text-20 font-semibold">{t("doneTitle")}</h1>
        <p className="text-14 text-ink-muted">{t("doneBody")}</p>
        <Button asChild size="lg" className="w-full">
          <Link href="/login">{t("signIn")}</Link>
        </Button>
      </div>
    )
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-2">
      <div>
        <h1 className="text-20 font-semibold">{t("title")}</h1>
        <p className="mt-0.5 text-14 text-ink-muted">{t("subtitle")}</p>
      </div>

      <FormAlert>{formError}</FormAlert>
      {linkRejected && (
        <Link href="/forgot-password" className="self-start text-14 text-brand underline-offset-4 hover:underline">
          {t("requestNew")}
        </Link>
      )}

      <TextField
        control={form.control}
        name="newPassword"
        label={t("newPassword")}
        description={t("passwordHint", { min: PASSWORD_MIN })}
        type="password"
        autoComplete="new-password"
        required
        autoFocus
      />
      <TextField
        control={form.control}
        name="confirmPassword"
        label={t("confirmPassword")}
        type="password"
        autoComplete="new-password"
        required
      />

      <Button type="submit" size="lg" disabled={form.formState.isSubmitting} className="mt-1 w-full">
        {form.formState.isSubmitting && <Spinner />}
        {t("submit")}
      </Button>
    </form>
  )
}

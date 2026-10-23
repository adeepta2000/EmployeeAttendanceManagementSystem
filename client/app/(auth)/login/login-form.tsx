"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import Link from "next/link"
import { useRouter } from "next/navigation"
import { useTranslations } from "next-intl"
import { useState } from "react"
import { useForm } from "react-hook-form"

import { applyApiErrors } from "@/components/forms/api-errors"
import { TextField } from "@/components/forms/fields"
import { FormAlert, FormNotice } from "@/components/forms/form-alert"
import { Button } from "@/components/ui/button"
import { Spinner } from "@/components/ui/spinner"
import { ApiError, auth } from "@/lib/api/client"
import { useErrorText } from "@/lib/api/errors"
import { safeNextPath } from "@/lib/safe-redirect"
import { loginSchema, type LoginInput } from "@/lib/schemas/auth"

type Props = {
  reason: "expired" | "signed-out" | "unavailable" | "password-changed" | null
  next: string | null
}

export function LoginForm({ reason, next }: Props) {
  const t = useTranslations("auth.login")
  const errorText = useErrorText()
  const router = useRouter()
  const [formError, setFormError] = useState<string | null>(null)

  const form = useForm<LoginInput>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  })

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)
    try {
      const result = await auth<{ mustChangePassword: boolean }>("login", values)
      const destination = safeNextPath(next)
      router.replace(
        result.mustChangePassword
          ? `/change-password?next=${encodeURIComponent(destination)}`
          : destination,
      )
      router.refresh()
    } catch (error) {
      form.resetField("password")
      if (applyApiErrors(error, form.setError, ["email", "password"])) return
      // One message for every failed sign-in, whatever the cause (§2.6): it never says whether the email exists.
      if (error instanceof ApiError && error.status === 401) setFormError(t("invalidCredentials"))
      else if (error instanceof ApiError && error.status === 429) setFormError(t("rateLimited"))
      else setFormError(errorText(error))
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-2">
      <div>
        <h1 className="text-20 font-semibold">{t("title")}</h1>
        <p className="mt-0.5 text-14 text-ink-muted">{t("subtitle")}</p>
      </div>

      {reason && !formError && <FormNotice>{t(`reason.${reason}`)}</FormNotice>}
      <FormAlert>{formError}</FormAlert>

      <TextField
        control={form.control}
        name="email"
        label={t("email")}
        type="email"
        autoComplete="username"
        inputMode="email"
        required
        autoFocus
      />
      <TextField
        control={form.control}
        name="password"
        label={t("password")}
        type="password"
        autoComplete="current-password"
        required
      />

      <Button type="submit" size="lg" disabled={form.formState.isSubmitting} className="mt-1 w-full">
        {form.formState.isSubmitting && <Spinner />}
        {form.formState.isSubmitting ? t("submitting") : t("submit")}
      </Button>

      <Link href="/forgot-password" className="self-start text-14 text-brand underline-offset-4 hover:underline">
        {t("forgot")}
      </Link>
    </form>
  )
}

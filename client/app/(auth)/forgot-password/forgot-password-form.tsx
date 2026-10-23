"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { MailCheckIcon } from "lucide-react"
import Link from "next/link"
import { useTranslations } from "next-intl"
import { useState } from "react"
import { useForm } from "react-hook-form"

import { applyApiErrors } from "@/components/forms/api-errors"
import { TextField } from "@/components/forms/fields"
import { FormAlert } from "@/components/forms/form-alert"
import { Button } from "@/components/ui/button"
import { Spinner } from "@/components/ui/spinner"
import { auth } from "@/lib/api/client"
import { useErrorText } from "@/lib/api/errors"
import { forgotPasswordSchema, type ForgotPasswordInput } from "@/lib/schemas/auth"

export function ForgotPasswordForm() {
  const t = useTranslations("auth.forgot")
  const errorText = useErrorText()
  const [sent, setSent] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const form = useForm<ForgotPasswordInput>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: "" },
  })

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)
    try {
      await auth("forgot-password", values)
      setSent(true)
    } catch (error) {
      if (!applyApiErrors(error, form.setError, ["email"])) setFormError(errorText(error))
    }
  })

  if (sent) {
    // The same answer whether or not the address has an account, so nobody can probe for accounts.
    return (
      <div className="flex flex-col gap-2" role="status">
        <MailCheckIcon aria-hidden="true" className="size-4 text-brand" />
        <h1 className="text-20 font-semibold">{t("sentTitle")}</h1>
        <p className="text-14 text-ink-muted">{t("sentBody")}</p>
        <Link href="/login" className="self-start text-14 text-brand underline-offset-4 hover:underline">
          {t("backToLogin")}
        </Link>
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

      <Button type="submit" size="lg" disabled={form.formState.isSubmitting} className="mt-1 w-full">
        {form.formState.isSubmitting && <Spinner />}
        {t("submit")}
      </Button>

      <Link href="/login" className="self-start text-14 text-brand underline-offset-4 hover:underline">
        {t("backToLogin")}
      </Link>
    </form>
  )
}

"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import Link from "next/link"
import { useRouter } from "next/navigation"
import { useTranslations } from "next-intl"
import { useState } from "react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"

import { applyApiErrors } from "@/components/forms/api-errors"
import { TextField } from "@/components/forms/fields"
import { FormAlert, FormNotice } from "@/components/forms/form-alert"
import { SignOutButton } from "@/components/shell/sign-out-button"
import { Button } from "@/components/ui/button"
import { Spinner } from "@/components/ui/spinner"
import { auth } from "@/lib/api/client"
import { useErrorText } from "@/lib/api/errors"
import { safeNextPath } from "@/lib/safe-redirect"
import { changePasswordSchema, PASSWORD_MIN, type ChangePasswordInput } from "@/lib/schemas/auth"

export function ChangePasswordForm({ forced, next }: { forced: boolean; next: string | null }) {
  const t = useTranslations("auth.change")
  const errorText = useErrorText()
  const router = useRouter()
  const [formError, setFormError] = useState<string | null>(null)

  const form = useForm<ChangePasswordInput>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  })

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)
    try {
      await auth("change-password", values)
      toast.success(t("done"))
      router.replace(safeNextPath(next))
      router.refresh()
    } catch (error) {
      if (!applyApiErrors(error, form.setError, ["currentPassword", "newPassword", "confirmPassword"], {
        "auth.invalid_current_password": "currentPassword",
      })) {
        setFormError(errorText(error))
      }
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-2">
      <div>
        <h1 className="text-20 font-semibold">{t("title")}</h1>
        <p className="mt-0.5 text-14 text-ink-muted">{t("subtitle")}</p>
      </div>

      {forced && <FormNotice>{t("forced")}</FormNotice>}
      <FormAlert>{formError}</FormAlert>

      <TextField
        control={form.control}
        name="currentPassword"
        label={t("currentPassword")}
        type="password"
        autoComplete="current-password"
        required
        autoFocus
      />
      <TextField
        control={form.control}
        name="newPassword"
        label={t("newPassword")}
        description={t("passwordHint", { min: PASSWORD_MIN })}
        type="password"
        autoComplete="new-password"
        required
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

      {forced ? (
        <SignOutButton variant="link" className="self-start px-0">
          {t("signOutInstead")}
        </SignOutButton>
      ) : (
        <Link href={safeNextPath(next)} className="self-start text-14 text-brand underline-offset-4 hover:underline">
          {t("cancel")}
        </Link>
      )}
    </form>
  )
}

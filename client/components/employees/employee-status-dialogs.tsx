"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { useTranslations } from "next-intl"
import { useState } from "react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"

import { applyApiErrors } from "@/components/forms/api-errors"
import { TextField } from "@/components/forms/fields"
import { FormAlert } from "@/components/forms/form-alert"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Spinner } from "@/components/ui/spinner"
import { useErrorText } from "@/lib/api/errors"
import { useSetEmployeeActive } from "@/lib/api/queries"
import type { Employee } from "@/lib/api/types"
import { todayInBusinessZone } from "@/lib/dates"
import { deactivateEmployeeSchema } from "@/lib/schemas/organization"

type DialogProps = { employee: Employee; open: boolean; onOpenChange: (open: boolean) => void }

/**
 * Records a separation (FR-1.5). Nothing is deleted: the history stays and the login is switched off.
 * The dialog spells that out before the user confirms.
 */
export function DeactivateEmployeeDialog({ employee, open, onOpenChange }: DialogProps) {
  const t = useTranslations("employees.deactivate")
  const errorText = useErrorText()
  const mutation = useSetEmployeeActive(employee.publicId)
  const [formError, setFormError] = useState<string | null>(null)

  const form = useForm({
    resolver: zodResolver(deactivateEmployeeSchema),
    defaultValues: { separationDate: todayInBusinessZone() },
  })

  const onSubmit = form.handleSubmit(async ({ separationDate }) => {
    setFormError(null)
    try {
      await mutation.mutateAsync({ active: false, separationDate })
      toast.success(t("done", { name: employee.fullName }))
      onOpenChange(false)
    } catch (error) {
      if (!applyApiErrors(error, form.setError, ["separationDate"], { "employee.separation_before_joining": "separationDate" })) {
        setFormError(errorText(error))
      }
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[440px]">
        <form onSubmit={onSubmit} noValidate className="flex flex-col gap-2">
          <DialogHeader>
            <DialogTitle>{t("title", { name: employee.fullName })}</DialogTitle>
            <DialogDescription>
              {employee.hasUserAccount ? t("descriptionWithLogin") : t("description")}
            </DialogDescription>
          </DialogHeader>
          <FormAlert>{formError}</FormAlert>
          <TextField
            control={form.control}
            name="separationDate"
            label={t("separationDate")}
            type="date"
            required
            autoFocus
          />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {t("cancel")}
            </Button>
            <Button type="submit" variant="destructive" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting && <Spinner />}
              {t("confirm")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

export function ReactivateEmployeeDialog({ employee, open, onOpenChange }: DialogProps) {
  const t = useTranslations("employees.reactivate")
  const errorText = useErrorText()
  const mutation = useSetEmployeeActive(employee.publicId)

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[440px]">
        <DialogHeader>
          <DialogTitle>{t("title", { name: employee.fullName })}</DialogTitle>
          <DialogDescription>{employee.hasUserAccount ? t("descriptionWithLogin") : t("description")}</DialogDescription>
        </DialogHeader>
        {mutation.isError && <FormAlert>{errorText(mutation.error)}</FormAlert>}
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            {t("cancel")}
          </Button>
          <Button
            type="button"
            disabled={mutation.isPending}
            onClick={async () => {
              try {
                await mutation.mutateAsync({ active: true })
                toast.success(t("done", { name: employee.fullName }))
                onOpenChange(false)
              } catch {
                // Shown above from mutation.error.
              }
            }}
          >
            {mutation.isPending && <Spinner />}
            {t("confirm")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

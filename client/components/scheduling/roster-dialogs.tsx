"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { differenceInCalendarDays, format, parseISO } from "date-fns"
import { useTranslations } from "next-intl"
import { useState } from "react"
import { useForm, useWatch, type Control, type FieldValues, type Path } from "react-hook-form"
import { toast } from "sonner"

import { applyApiErrors } from "@/components/forms/api-errors"
import { SelectField, TextField, type Option } from "@/components/forms/fields"
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
import { useDepartments } from "@/lib/api/queries"
import { useRosterMutations } from "@/lib/api/scheduling-queries"
import type { Shift } from "@/lib/api/scheduling-types"
import { MAX_RANGE_DAYS, bulkRosterSchema, type BulkRosterInput, type BulkRosterOutput } from "@/lib/schemas/scheduling"
import { formatTime, parseTime } from "@/lib/shift-time"

/** The day-type choice of a roster entry, as the select shows it; the API's nullable flag underneath. */
export const OVERRIDES = ["pattern", "working", "off"] as const
export type OverrideChoice = (typeof OVERRIDES)[number]

export function toOverride(choice: OverrideChoice): boolean | null {
  return choice === "pattern" ? null : choice === "off"
}

/** Active shifts as select options: "N · Night (22:00–06:00)". */
export function shiftOptions(shifts: Shift[] | undefined): Option[] {
  return (shifts ?? [])
    .filter((shift) => shift.isActive)
    .map((shift) => ({
      value: shift.publicId,
      label: `${shift.code} · ${shift.name} (${formatTime(parseTime(shift.startTime) ?? 0)}–${formatTime(parseTime(shift.endTime) ?? 0)})`,
    }))
}

/** "Follow the weekly-off pattern / Working day / Weekly off", bound to a boolean-or-null field. */
export function OverrideField<T extends FieldValues>({ control, name }: { control: Control<T>; name: Path<T> }) {
  const t = useTranslations("scheduling.roster.override")
  const value = useWatch({ control, name }) as boolean | null | undefined
  return (
    <SelectField
      control={control}
      name={name}
      label={t("label")}
      description={t(`hint.${value === true ? "off" : value === false ? "working" : "pattern"}`)}
      // The select holds text; the field holds null, false or true.
      options={OVERRIDES.map((choice) => ({ value: choice, label: t(choice) }))}
      valueAs={{
        toField: (choice) => toOverride(choice as OverrideChoice),
        fromField: (field) => (field === true ? "off" : field === false ? "working" : "pattern"),
      }}
    />
  )
}

/**
 * FR-2.4: one shift for every active employee of a department over a date range of up to 92 days. It is
 * a big change made in one statement, so the dialog says exactly what it will and won't touch first.
 */
export function BulkRosterDialog({
  shifts,
  defaults,
  onClose,
}: {
  shifts: Shift[] | undefined
  defaults: { departmentId?: string; fromDate: string; toDate: string }
  onClose: () => void
}) {
  const t = useTranslations("scheduling.roster.bulk")
  const tCommon = useTranslations("common")
  const errorText = useErrorText()
  const departments = useDepartments()
  const { bulk } = useRosterMutations()
  const [formError, setFormError] = useState<string | null>(null)

  const form = useForm<BulkRosterInput, unknown, BulkRosterOutput>({
    resolver: zodResolver(bulkRosterSchema),
    defaultValues: {
      departmentId: defaults.departmentId ?? "",
      fromDate: defaults.fromDate,
      toDate: defaults.toDate,
      shiftId: "",
      isWeeklyOffOverride: null,
      remarks: "",
    },
  })
  const [departmentId, fromDate, toDate, shiftId] = useWatch({
    control: form.control,
    name: ["departmentId", "fromDate", "toDate", "shiftId"],
  })
  const days = differenceInCalendarDays(parseISO(toDate ?? ""), parseISO(fromDate ?? "")) + 1
  const department = departments.data?.find((d) => d.publicId === departmentId)
  const shift = shifts?.find((s) => s.publicId === shiftId)
  const departmentOptions = (departments.data ?? []).filter((d) => d.isActive).map((d) => ({ value: d.publicId, label: d.name }))

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)
    try {
      const result = await bulk.mutateAsync(values)
      toast.success(
        t("done", {
          shift: shift?.code ?? "",
          employees: result.employeeCount,
          days: result.dayCount,
          created: result.created,
          updated: result.updated,
        }),
      )
      onClose()
    } catch (error) {
      const applied = applyApiErrors(
        error,
        form.setError,
        ["departmentId", "fromDate", "toDate", "shiftId", "isWeeklyOffOverride", "remarks"],
        { "roster.department_not_found": "departmentId", "roster.shift_not_found": "shiftId" },
      )
      if (!applied) setFormError(errorText(error))
    }
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-[600px]">
        <form onSubmit={onSubmit} noValidate className="flex flex-col gap-2">
          <DialogHeader>
            <DialogTitle>{t("title")}</DialogTitle>
            <DialogDescription>{t("description", { max: MAX_RANGE_DAYS })}</DialogDescription>
          </DialogHeader>
          <FormAlert>{formError}</FormAlert>
          <SelectField
            control={form.control}
            name="departmentId"
            label={t("department")}
            options={departmentOptions}
            placeholder={t("chooseDepartment")}
            required
          />
          <div className="grid grid-cols-2 gap-2">
            <TextField control={form.control} name="fromDate" label={t("from")} type="date" mono required />
            <TextField control={form.control} name="toDate" label={t("to")} type="date" mono required />
          </div>
          <SelectField
            control={form.control}
            name="shiftId"
            label={t("shift")}
            options={shiftOptions(shifts)}
            placeholder={t("chooseShift")}
            required
          />
          <OverrideField control={form.control} name="isWeeklyOffOverride" />
          <TextField control={form.control} name="remarks" label={t("remarks")} description={t("remarksHint")} />

          <div className="rounded-md border border-rule bg-canvas px-1.5 py-1 text-13" aria-live="polite">
            <p className="font-medium">
              {department && shift && Number.isFinite(days) && days >= 1 && days <= MAX_RANGE_DAYS
                ? t("preview", {
                    shift: `${shift.code} · ${shift.name}`,
                    department: department.name,
                    days,
                    from: format(parseISO(fromDate!), "d MMM yyyy"),
                    to: format(parseISO(toDate!), "d MMM yyyy"),
                  })
                : t("previewIncomplete")}
            </p>
            <ul className="mt-0.5 list-disc pl-2 text-ink-muted">
              <li>{t("rules.scope")}</li>
              <li>{t("rules.overwrite")}</li>
              <li>{t("rules.joining")}</li>
            </ul>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {tCommon("cancel")}
            </Button>
            <Button type="submit" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting && <Spinner />}
              {Number.isFinite(days) && days >= 1 ? t("submitDays", { days }) : t("submit")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

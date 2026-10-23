"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { useTranslations } from "next-intl"
import { useEffect, useState, type ReactNode } from "react"
import { useForm, type FieldErrors, type FieldPath } from "react-hook-form"
import { toast } from "sonner"

import { applyApiErrors } from "@/components/forms/api-errors"
import { CustomField, SelectField, TextField, type Option } from "@/components/forms/fields"
import { FormAlert } from "@/components/forms/form-alert"
import { Button } from "@/components/ui/button"
import { Spinner } from "@/components/ui/spinner"
import { useErrorText } from "@/lib/api/errors"
import { useDepartments, useDesignations, useSaveEmployee, useWorkLocations } from "@/lib/api/queries"
import { EmploymentTypes, Genders, type Employee, type OrgUnitRef } from "@/lib/api/types"
import { employeeSchema, type EmployeeInput, type EmployeeOutput } from "@/lib/schemas/organization"

import { EmployeeFrame, Section, type EmployeeTab } from "./employee-frame"
import { EmployeePicker } from "./employee-picker"

const PROFILE_FIELDS = ["firstName", "lastName", "dateOfBirth", "gender", "email", "phone"] as const
const EMPLOYMENT_FIELDS = [
  "employeeCode",
  "employmentType",
  "departmentId",
  "designationId",
  "workLocationId",
  "reportingManagerId",
  "joiningDate",
  "confirmationDate",
] as const
const ALL_FIELDS: FieldPath<EmployeeInput>[] = [...PROFILE_FIELDS, ...EMPLOYMENT_FIELDS]

/** API error codes that belong to one field (src/Attendance.Application/Organization/OrganizationErrors.cs). */
const CODE_FIELDS: Record<string, FieldPath<EmployeeInput>> = {
  "employee.code_taken": "employeeCode",
  "employee.department_not_found": "departmentId",
  "employee.designation_not_found": "designationId",
  "employee.work_location_not_found": "workLocationId",
  "employee.manager_not_found": "reportingManagerId",
  "employee.manager_cycle": "reportingManagerId",
}

function tabOf(field: string): EmployeeTab {
  return (PROFILE_FIELDS as readonly string[]).includes(field) ? "profile" : "employment"
}

function toFormValues(employee?: Employee): EmployeeInput {
  return {
    employeeCode: employee?.employeeCode ?? "",
    firstName: employee?.firstName ?? "",
    lastName: employee?.lastName ?? "",
    dateOfBirth: employee?.dateOfBirth ?? "",
    gender: employee?.gender ?? "",
    email: employee?.email ?? "",
    phone: employee?.phone ?? "",
    joiningDate: employee?.joiningDate ?? "",
    confirmationDate: employee?.confirmationDate ?? "",
    employmentType: employee?.employmentType ?? "",
    departmentId: employee?.department.publicId ?? "",
    designationId: employee?.designation.publicId ?? "",
    workLocationId: employee?.workLocation.publicId ?? "",
    reportingManagerId: employee?.reportingManager?.publicId ?? "",
  }
}

/**
 * Active units to choose from, plus the record's current one even if it has been retired since: the API
 * keeps an unchanged link to a retired unit, so the form must be able to show it.
 */
function unitOptions(
  units: { publicId: string; name: string; code: string; isActive: boolean }[] | undefined,
  current: OrgUnitRef | undefined,
  retiredLabel: (name: string) => string,
): Option[] {
  const options = (units ?? [])
    .filter((unit) => unit.isActive || unit.publicId === current?.publicId)
    .map((unit) => ({ value: unit.publicId, label: unit.isActive ? unit.name : retiredLabel(unit.name) }))
  if (current && !options.some((option) => option.value === current.publicId)) {
    options.unshift({ value: current.publicId, label: retiredLabel(current.name) })
  }
  return options
}

type Props = {
  /** The record being edited; absent when adding a new employee. */
  employee?: Employee
  title: ReactNode
  description?: ReactNode
  tab: EmployeeTab
  onTabChange: (tab: EmployeeTab) => void
  onCancel: () => void
  onSaved: (employee: Employee) => void
}

/** Adds or edits an employee (FR-1.2) across the Profile and Employment tabs of screen 27. */
export function EmployeeForm({ employee, title, description, tab, onTabChange, onCancel, onSaved }: Props) {
  const t = useTranslations("employees")
  const tFields = useTranslations("employees.fields")
  const errorText = useErrorText()
  const [formError, setFormError] = useState<string | null>(null)

  const departments = useDepartments()
  const designations = useDesignations()
  const locations = useWorkLocations()
  const save = useSaveEmployee(employee?.publicId)

  const form = useForm<EmployeeInput, unknown, EmployeeOutput>({
    resolver: zodResolver(employeeSchema),
    defaultValues: toFormValues(employee),
  })
  const { control, formState } = form

  // Opens the tab holding the first invalid field, then focuses it once that tab is visible: a field in a
  // hidden tab can't take focus, and the tab only changes after the URL does.
  const [pendingFocus, setPendingFocus] = useState<FieldPath<EmployeeInput> | null>(null)
  const revealFirstError = (errors: FieldErrors<EmployeeInput>) => {
    const first = ALL_FIELDS.find((field) => errors[field])
    if (!first) return
    onTabChange(tabOf(first))
    setPendingFocus(first)
  }
  useEffect(() => {
    if (!pendingFocus || tabOf(pendingFocus) !== tab) return
    form.setFocus(pendingFocus)
    // eslint-disable-next-line react-hooks/set-state-in-effect -- one-shot hand-off after the tab switch
    setPendingFocus(null)
  }, [pendingFocus, tab, form])

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)
    try {
      const saved = await save.mutateAsync(values)
      toast.success(employee ? t("form.saved") : t("form.created", { name: saved.fullName }))
      onSaved(saved)
    } catch (error) {
      if (applyApiErrors(error, form.setError, ALL_FIELDS, CODE_FIELDS)) revealFirstError(form.formState.errors)
      else setFormError(errorText(error))
    }
  }, revealFirstError)

  const tabsWithErrors = (["profile", "employment"] as const).filter((value) =>
    (value === "profile" ? PROFILE_FIELDS : EMPLOYMENT_FIELDS).some((field) => formState.errors[field]),
  )

  const retired = (name: string) => t("form.retired", { name })

  return (
    <form onSubmit={onSubmit} noValidate className="h-full">
      <EmployeeFrame
        title={title}
        description={description}
        tab={tab}
        onTabChange={onTabChange}
        disabledTabs={employee ? [] : ["shift", "leave", "documents"]}
        tabsWithErrors={tabsWithErrors}
        profile={
          <>
            <Section title={t("detail.sections.personal")}>
              <div className="grid grid-cols-2 gap-2">
                <TextField control={control} name="firstName" label={tFields("firstName")} autoComplete="off" required />
                <TextField control={control} name="lastName" label={tFields("lastName")} autoComplete="off" required />
                <TextField control={control} name="dateOfBirth" label={tFields("dateOfBirth")} type="date" required />
                <SelectField
                  control={control}
                  name="gender"
                  label={tFields("gender")}
                  placeholder={t("form.choose")}
                  options={Genders.map((gender) => ({ value: gender, label: t(`genders.${gender}`) }))}
                  required
                />
              </div>
            </Section>
            <Section title={t("detail.sections.contact")}>
              <div className="grid grid-cols-2 gap-2">
                <TextField
                  control={control}
                  name="email"
                  label={tFields("email")}
                  type="email"
                  autoComplete="off"
                  description={t("form.emailHint")}
                />
                <TextField
                  control={control}
                  name="phone"
                  label={tFields("phone")}
                  type="tel"
                  inputMode="tel"
                  autoComplete="off"
                />
              </div>
            </Section>
          </>
        }
        employment={
          <>
            <Section title={t("detail.sections.position")}>
              <div className="grid grid-cols-2 gap-2">
                <TextField
                  control={control}
                  name="employeeCode"
                  label={tFields("employeeCode")}
                  description={t("form.codeHint")}
                  autoComplete="off"
                  mono
                  required
                />
                <SelectField
                  control={control}
                  name="employmentType"
                  label={tFields("employmentType")}
                  placeholder={t("form.choose")}
                  options={EmploymentTypes.map((type) => ({ value: type, label: t(`employmentTypes.${type}`) }))}
                  required
                />
                <SelectField
                  control={control}
                  name="departmentId"
                  label={tFields("department")}
                  placeholder={departments.isPending ? t("form.loading") : t("form.choose")}
                  options={unitOptions(departments.data, employee?.department, retired)}
                  required
                />
                <SelectField
                  control={control}
                  name="designationId"
                  label={tFields("designation")}
                  placeholder={designations.isPending ? t("form.loading") : t("form.choose")}
                  options={unitOptions(designations.data, employee?.designation, retired)}
                  required
                />
                <SelectField
                  control={control}
                  name="workLocationId"
                  label={tFields("workLocation")}
                  placeholder={locations.isPending ? t("form.loading") : t("form.choose")}
                  options={unitOptions(locations.data, employee?.workLocation, retired)}
                  required
                />
                <CustomField
                  control={control}
                  name="reportingManagerId"
                  label={tFields("reportingManager")}
                  render={(field, controlProps) => (
                    <EmployeePicker
                      {...controlProps}
                      value={(field.value as string | null) ?? ""}
                      onChange={field.onChange}
                      onBlur={field.onBlur}
                      initial={employee?.reportingManager}
                      excludeIds={employee ? [employee.publicId] : []}
                      placeholder={t("form.noManager")}
                    />
                  )}
                />
              </div>
            </Section>
            <Section title={t("detail.sections.dates")}>
              <div className="grid grid-cols-2 gap-2">
                <TextField control={control} name="joiningDate" label={tFields("joiningDate")} type="date" required />
                <TextField
                  control={control}
                  name="confirmationDate"
                  label={tFields("confirmationDate")}
                  type="date"
                  description={t("form.confirmationHint")}
                />
              </div>
            </Section>
          </>
        }
        footer={
          <div className="flex shrink-0 items-center gap-2 border-t border-rule bg-surface px-3 py-1.5">
            <div className="min-w-0 flex-1">
              <FormAlert>{formError}</FormAlert>
            </div>
            <Button type="button" variant="outline" onClick={onCancel} disabled={formState.isSubmitting}>
              {t("form.cancel")}
            </Button>
            <Button type="submit" disabled={formState.isSubmitting}>
              {formState.isSubmitting && <Spinner />}
              {employee ? t("form.save") : t("form.create")}
            </Button>
          </div>
        }
      />
    </form>
  )
}

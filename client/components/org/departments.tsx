"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { getCoreRowModel, useReactTable, type ColumnDef } from "@tanstack/react-table"
import { ArchiveIcon, ArchiveRestoreIcon, PencilIcon } from "lucide-react"
import Link from "next/link"
import { useTranslations } from "next-intl"
import { useMemo, useState } from "react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"

import { DataTable } from "@/components/data-table/data-table"
import { ActiveBadge } from "@/components/employees/active-badge"
import { EmployeePicker } from "@/components/employees/employee-picker"
import { applyApiErrors } from "@/components/forms/api-errors"
import { CustomField, SelectField, TextField } from "@/components/forms/fields"
import { FormAlert } from "@/components/forms/form-alert"
import { useSession } from "@/components/shell/session-context"
import { Button } from "@/components/ui/button"
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Spinner } from "@/components/ui/spinner"
import { useErrorText } from "@/lib/api/errors"
import { useDepartmentMutations, useDepartments } from "@/lib/api/queries"
import type { Department } from "@/lib/api/types"
import { Permissions } from "@/lib/permissions"
import { departmentSchema, type DepartmentInput, type DepartmentOutput } from "@/lib/schemas/organization"

import { ListMessage, MasterDataToolbar, SetActiveDialog, useMasterDataFilter } from "./org-structure"

type DialogState = { kind: "edit"; department?: Department } | { kind: "setActive"; department: Department } | null

const searchExtra = (department: Department) =>
  `${department.parentDepartment?.name ?? ""} ${department.headEmployee?.fullName ?? ""}`

/** Screen 28, departments (FR-1.1). Anyone signed in can read them; changing them needs org.write. */
export function Departments() {
  const t = useTranslations("org.departments")
  const tColumns = useTranslations("org.columns")
  const errorText = useErrorText()
  const { can } = useSession()
  const canWrite = can(Permissions.Org.Write)
  const departments = useDepartments()
  const { params, setParams, filtered } = useMasterDataFilter(departments.data, searchExtra)
  const [dialog, setDialog] = useState<DialogState>(null)
  const { setActive } = useDepartmentMutations()

  const columns = useMemo<ColumnDef<Department>[]>(
    () => [
      {
        id: "code",
        header: tColumns("code"),
        meta: { className: "w-[120px]", skeletonWidth: "w-6" },
        cell: ({ row }) => <span className="font-mono">{row.original.code}</span>,
      },
      {
        id: "name",
        header: tColumns("name"),
        meta: { skeletonWidth: "w-18" },
        cell: ({ row }) => <span className="font-medium">{row.original.name}</span>,
      },
      {
        id: "parent",
        header: t("columns.parent"),
        meta: { skeletonWidth: "w-14" },
        cell: ({ row }) => row.original.parentDepartment?.name ?? <span className="text-ink-muted">—</span>,
      },
      {
        id: "head",
        header: t("columns.head"),
        meta: { skeletonWidth: "w-14" },
        cell: ({ row }) =>
          row.original.headEmployee ? (
            <Link
              href={`/employees/${row.original.headEmployee.publicId}`}
              className="hover:text-brand hover:underline"
            >
              {row.original.headEmployee.fullName}
            </Link>
          ) : (
            <span className="text-ink-muted">—</span>
          ),
      },
      {
        id: "status",
        header: tColumns("status"),
        meta: { className: "w-[96px]", skeletonWidth: "w-7" },
        cell: ({ row }) => <ActiveBadge active={row.original.isActive} />,
      },
      ...(canWrite
        ? [
            {
              id: "actions",
              header: () => <span className="sr-only">{tColumns("actions")}</span>,
              meta: { className: "w-[112px] text-right", skeletonWidth: "w-8 ml-auto" },
              cell: ({ row }) => (
                <RowActions
                  name={row.original.name}
                  active={row.original.isActive}
                  onEdit={() => setDialog({ kind: "edit", department: row.original })}
                  onSetActive={() => setDialog({ kind: "setActive", department: row.original })}
                />
              ),
            } satisfies ColumnDef<Department>,
          ]
        : []),
    ],
    [t, tColumns, canWrite],
  )

  // The React Compiler isn't enabled; TanStack Table memoizes its own row models.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: filtered,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getRowId: (row) => row.publicId,
    enableSorting: false,
  })

  const target = dialog?.kind === "setActive" ? dialog.department : null

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <MasterDataToolbar
        params={params}
        setParams={setParams}
        addLabel={canWrite ? t("add") : undefined}
        onAdd={() => setDialog({ kind: "edit" })}
      />

      {departments.isError ? (
        <ListMessage>
          <FormAlert>{errorText(departments.error)}</FormAlert>
        </ListMessage>
      ) : (
        <DataTable
          table={table}
          caption={t("caption")}
          isLoading={departments.isPending}
          skeletonRows={8}
          className="min-h-0 flex-1 bg-surface"
          empty={<p className="text-14 text-ink-muted">{params.q ? t("emptyFiltered") : t(`empty.${params.status}`)}</p>}
        />
      )}

      {dialog?.kind === "edit" && (
        <DepartmentDialog
          department={dialog.department}
          departments={departments.data ?? []}
          onClose={() => setDialog(null)}
        />
      )}

      {target && (
        <SetActiveDialog
          open
          onOpenChange={(open) => !open && setDialog(null)}
          retiring={target.isActive}
          title={t(target.isActive ? "retire.title" : "restore.title", { name: target.name })}
          description={t(target.isActive ? "retire.description" : "restore.description")}
          confirmLabel={t(target.isActive ? "retire.confirm" : "restore.confirm")}
          onConfirm={async () => {
            await setActive.mutateAsync({ id: target.publicId, active: !target.isActive })
            toast.success(t(target.isActive ? "retire.done" : "restore.done", { name: target.name }))
          }}
        />
      )}
    </div>
  )
}

export function RowActions({
  name,
  active,
  onEdit,
  onSetActive,
}: {
  name: string
  active: boolean
  onEdit: () => void
  onSetActive: () => void
}) {
  const t = useTranslations("org.actions")
  return (
    <div className="flex justify-end gap-0.25">
      <Button variant="ghost" size="icon-sm" onClick={onEdit} aria-label={t("edit", { name })}>
        <PencilIcon aria-hidden="true" />
      </Button>
      <Button
        variant="ghost"
        size="icon-sm"
        onClick={onSetActive}
        aria-label={active ? t("retire", { name }) : t("restore", { name })}
      >
        {active ? <ArchiveIcon aria-hidden="true" /> : <ArchiveRestoreIcon aria-hidden="true" />}
      </Button>
    </div>
  )
}

function DepartmentDialog({
  department,
  departments,
  onClose,
}: {
  department?: Department
  departments: Department[]
  onClose: () => void
}) {
  const t = useTranslations("org.departments")
  const tColumns = useTranslations("org.columns")
  const tCommon = useTranslations("common")
  const errorText = useErrorText()
  const { save } = useDepartmentMutations()
  const [formError, setFormError] = useState<string | null>(null)

  const form = useForm<DepartmentInput, unknown, DepartmentOutput>({
    resolver: zodResolver(departmentSchema),
    defaultValues: {
      code: department?.code ?? "",
      name: department?.name ?? "",
      parentDepartmentId: department?.parentDepartment?.publicId ?? "",
      headEmployeeId: department?.headEmployee?.publicId ?? "",
    },
  })

  // A department can't sit under itself; deeper loops are the API's to refuse (department.parent_cycle).
  const parentOptions = departments
    .filter((d) => d.publicId !== department?.publicId)
    .filter((d) => d.isActive || d.publicId === department?.parentDepartment?.publicId)
    .map((d) => ({ value: d.publicId, label: d.isActive ? d.name : t("retiredOption", { name: d.name }) }))

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)
    try {
      await save.mutateAsync({ id: department?.publicId, values })
      toast.success(department ? t("saved", { name: values.name }) : t("created", { name: values.name }))
      onClose()
    } catch (error) {
      const applied = applyApiErrors(error, form.setError, ["code", "name", "parentDepartmentId", "headEmployeeId"], {
        "department.code_taken": "code",
        "department.parent_not_found": "parentDepartmentId",
        "department.parent_cycle": "parentDepartmentId",
        "department.head_not_found": "headEmployeeId",
      })
      if (!applied) setFormError(errorText(error))
    }
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-[520px]">
        <form onSubmit={onSubmit} noValidate className="flex flex-col gap-2">
          <DialogHeader>
            <DialogTitle>{department ? t("editTitle", { name: department.name }) : t("addTitle")}</DialogTitle>
          </DialogHeader>
          <FormAlert>{formError}</FormAlert>
          <div className="grid grid-cols-[160px_1fr] gap-2">
            <TextField control={form.control} name="code" label={tColumns("code")} mono required autoFocus />
            <TextField control={form.control} name="name" label={tColumns("name")} required />
          </div>
          <SelectField
            control={form.control}
            name="parentDepartmentId"
            label={t("columns.parent")}
            noneLabel={t("noParent")}
            options={parentOptions}
          />
          <CustomField
            control={form.control}
            name="headEmployeeId"
            label={t("columns.head")}
            description={t("headHint")}
            render={(field, control) => (
              <EmployeePicker
                {...control}
                value={(field.value as string | null) ?? ""}
                onChange={field.onChange}
                onBlur={field.onBlur}
                initial={department?.headEmployee}
                placeholder={t("noHead")}
              />
            )}
          />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {tCommon("cancel")}
            </Button>
            <Button type="submit" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting && <Spinner />}
              {department ? tCommon("save") : t("add")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

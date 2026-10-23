"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { getCoreRowModel, useReactTable, type ColumnDef } from "@tanstack/react-table"
import { useTranslations } from "next-intl"
import { useMemo, useState } from "react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"

import { DataTable } from "@/components/data-table/data-table"
import { ActiveBadge } from "@/components/employees/active-badge"
import { applyApiErrors } from "@/components/forms/api-errors"
import { TextField } from "@/components/forms/fields"
import { FormAlert } from "@/components/forms/form-alert"
import { useSession } from "@/components/shell/session-context"
import { Button } from "@/components/ui/button"
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Spinner } from "@/components/ui/spinner"
import { useErrorText } from "@/lib/api/errors"
import { useDesignationMutations, useDesignations } from "@/lib/api/queries"
import type { Designation } from "@/lib/api/types"
import { Permissions } from "@/lib/permissions"
import { designationSchema, type DesignationInput, type DesignationOutput } from "@/lib/schemas/organization"

import { RowActions } from "./departments"
import { ListMessage, MasterDataToolbar, SetActiveDialog, useMasterDataFilter } from "./org-structure"

type DialogState = { kind: "edit"; designation?: Designation } | { kind: "setActive"; designation: Designation } | null

const searchExtra = (designation: Designation) => designation.grade ?? ""

/** Screen 28, designations (FR-1.1): job titles with an optional pay grade. */
export function Designations() {
  const t = useTranslations("org.designations")
  const tColumns = useTranslations("org.columns")
  const errorText = useErrorText()
  const { can } = useSession()
  const canWrite = can(Permissions.Org.Write)
  const designations = useDesignations()
  const { params, setParams, filtered } = useMasterDataFilter(designations.data, searchExtra)
  const [dialog, setDialog] = useState<DialogState>(null)
  const { setActive } = useDesignationMutations()

  const columns = useMemo<ColumnDef<Designation>[]>(
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
        id: "grade",
        header: t("columns.grade"),
        meta: { className: "w-[120px]", skeletonWidth: "w-5" },
        cell: ({ row }) => row.original.grade ?? <span className="text-ink-muted">—</span>,
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
                  onEdit={() => setDialog({ kind: "edit", designation: row.original })}
                  onSetActive={() => setDialog({ kind: "setActive", designation: row.original })}
                />
              ),
            } satisfies ColumnDef<Designation>,
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

  const target = dialog?.kind === "setActive" ? dialog.designation : null

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <MasterDataToolbar
        params={params}
        setParams={setParams}
        addLabel={canWrite ? t("add") : undefined}
        onAdd={() => setDialog({ kind: "edit" })}
      />

      {designations.isError ? (
        <ListMessage>
          <FormAlert>{errorText(designations.error)}</FormAlert>
        </ListMessage>
      ) : (
        <DataTable
          table={table}
          caption={t("caption")}
          isLoading={designations.isPending}
          skeletonRows={12}
          className="min-h-0 flex-1 bg-surface"
          empty={<p className="text-14 text-ink-muted">{params.q ? t("emptyFiltered") : t(`empty.${params.status}`)}</p>}
        />
      )}

      {dialog?.kind === "edit" && <DesignationDialog designation={dialog.designation} onClose={() => setDialog(null)} />}

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

function DesignationDialog({ designation, onClose }: { designation?: Designation; onClose: () => void }) {
  const t = useTranslations("org.designations")
  const tColumns = useTranslations("org.columns")
  const tCommon = useTranslations("common")
  const errorText = useErrorText()
  const { save } = useDesignationMutations()
  const [formError, setFormError] = useState<string | null>(null)

  const form = useForm<DesignationInput, unknown, DesignationOutput>({
    resolver: zodResolver(designationSchema),
    defaultValues: { code: designation?.code ?? "", name: designation?.name ?? "", grade: designation?.grade ?? "" },
  })

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)
    try {
      await save.mutateAsync({ id: designation?.publicId, values })
      toast.success(designation ? t("saved", { name: values.name }) : t("created", { name: values.name }))
      onClose()
    } catch (error) {
      if (!applyApiErrors(error, form.setError, ["code", "name", "grade"], { "designation.code_taken": "code" })) {
        setFormError(errorText(error))
      }
    }
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-[520px]">
        <form onSubmit={onSubmit} noValidate className="flex flex-col gap-2">
          <DialogHeader>
            <DialogTitle>{designation ? t("editTitle", { name: designation.name }) : t("addTitle")}</DialogTitle>
          </DialogHeader>
          <FormAlert>{formError}</FormAlert>
          <div className="grid grid-cols-[160px_1fr] gap-2">
            <TextField control={form.control} name="code" label={tColumns("code")} mono required autoFocus />
            <TextField control={form.control} name="name" label={tColumns("name")} required />
          </div>
          <TextField
            control={form.control}
            name="grade"
            label={t("columns.grade")}
            description={t("gradeHint")}
            className="max-w-[240px]"
          />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {tCommon("cancel")}
            </Button>
            <Button type="submit" disabled={form.formState.isSubmitting}>
              {form.formState.isSubmitting && <Spinner />}
              {designation ? tCommon("save") : t("add")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

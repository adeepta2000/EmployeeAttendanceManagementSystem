"use client"

import { getCoreRowModel, useReactTable, type ColumnDef } from "@tanstack/react-table"
import { MoonIcon } from "lucide-react"
import { useRouter } from "next/navigation"
import { useTranslations } from "next-intl"
import { useMemo, useState } from "react"
import { toast } from "sonner"

import { DataTable } from "@/components/data-table/data-table"
import { ActiveBadge } from "@/components/employees/active-badge"
import { FormAlert } from "@/components/forms/form-alert"
import { RowActions } from "@/components/org/departments"
import { ListMessage, MasterDataToolbar, SetActiveDialog, useMasterDataFilter } from "@/components/org/org-structure"
import { PageHeader } from "@/components/page-header"
import { useSession } from "@/components/shell/session-context"
import { useErrorText } from "@/lib/api/errors"
import { useShiftMutations, useShifts } from "@/lib/api/scheduling-queries"
import type { Shift } from "@/lib/api/scheduling-types"
import { Permissions } from "@/lib/permissions"
import { formatHours, formatTime, parseTime } from "@/lib/shift-time"

import { ShiftDayBar } from "./shift-timeline"

/** Screen 29, the list (FR-2.1). Anyone signed in can read shifts; changing them needs shift.write. */
export function ShiftList() {
  const t = useTranslations("scheduling.shifts")
  const tColumns = useTranslations("org.columns")
  const errorText = useErrorText()
  const router = useRouter()
  const { can } = useSession()
  const canWrite = can(Permissions.Shift.Write)
  const shifts = useShifts()
  const { params, setParams, filtered } = useMasterDataFilter(shifts.data)
  const [target, setTarget] = useState<Shift | null>(null)
  const { setActive } = useShiftMutations()

  const columns = useMemo<ColumnDef<Shift>[]>(
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
        meta: { skeletonWidth: "w-14" },
        cell: ({ row }) => <span className="font-medium">{row.original.name}</span>,
      },
      {
        id: "hours",
        header: t("columns.hours"),
        meta: { className: "w-[300px]", skeletonWidth: "w-24" },
        cell: ({ row }) => <Hours shift={row.original} />,
      },
      {
        id: "break",
        header: t("columns.break"),
        meta: { className: "w-[88px] text-right", skeletonWidth: "w-5 ml-auto" },
        cell: ({ row }) => <span className="font-mono">{t("minutes", { minutes: row.original.breakMinutes })}</span>,
      },
      {
        id: "grace",
        header: t("columns.grace"),
        meta: { className: "w-[112px] text-right", skeletonWidth: "w-6 ml-auto" },
        cell: ({ row }) => (
          <span className="font-mono" title={t("columns.graceTitle")}>
            {row.original.graceInMinutes} / {row.original.graceOutMinutes}
          </span>
        ),
      },
      {
        id: "days",
        header: t("columns.days"),
        meta: { className: "w-[120px] text-right", skeletonWidth: "w-7 ml-auto" },
        cell: ({ row }) => (
          <span className="font-mono" title={t("columns.daysTitle")}>
            {formatHours(row.original.fullDayMinutes)} / {formatHours(row.original.halfDayThresholdMinutes)}
          </span>
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
                  onEdit={() => router.push(`/setup/shifts/${row.original.publicId}`)}
                  onSetActive={() => setTarget(row.original)}
                />
              ),
            } satisfies ColumnDef<Shift>,
          ]
        : []),
    ],
    [t, tColumns, canWrite, router],
  )

  // eslint-disable-next-line react-hooks/incompatible-library -- TanStack Table memoizes its own row models
  const table = useReactTable({
    data: filtered,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getRowId: (row) => row.publicId,
    enableSorting: false,
  })

  return (
    <div className="flex h-full flex-col">
      <PageHeader title={t("title")} description={t("description")} />
      <MasterDataToolbar
        params={params}
        setParams={setParams}
        addLabel={canWrite ? t("add") : undefined}
        onAdd={() => router.push("/setup/shifts/new")}
      />
      {shifts.isError ? (
        <ListMessage>
          <FormAlert>{errorText(shifts.error)}</FormAlert>
        </ListMessage>
      ) : (
        <DataTable
          table={table}
          caption={t("caption")}
          isLoading={shifts.isPending}
          skeletonRows={6}
          className="min-h-0 flex-1 bg-surface"
          empty={<p className="text-14 text-ink-muted">{params.q ? t("emptyFiltered") : t(`empty.${params.status}`)}</p>}
        />
      )}

      {target && (
        <SetActiveDialog
          open
          onOpenChange={(open) => !open && setTarget(null)}
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

/** "22:00 – 06:00 +1" with the shift's place in the day beside it. */
function Hours({ shift }: { shift: Shift }) {
  const t = useTranslations("scheduling.shifts")
  const start = parseTime(shift.startTime) ?? 0
  const end = parseTime(shift.endTime) ?? 0
  return (
    <div className="flex items-center gap-1.5">
      <span className="w-[112px] shrink-0 font-mono whitespace-nowrap">
        {formatTime(start)}–{formatTime(end)}
        {shift.crossesMidnight && (
          <sup className="ml-0.25 text-12 font-semibold text-ink-muted" aria-hidden="true">
            +1
          </sup>
        )}
      </span>
      <ShiftDayBar start={start} end={end} className="w-[120px]" />
      <span className="flex items-center gap-0.25 text-12 text-ink-muted">
        {shift.crossesMidnight && <MoonIcon aria-hidden="true" className="size-1.5" />}
        <span className="font-mono">{formatHours(shift.durationMinutes)}</span>
        {shift.crossesMidnight && <span className="sr-only">{t("nightShift")}</span>}
      </span>
    </div>
  )
}

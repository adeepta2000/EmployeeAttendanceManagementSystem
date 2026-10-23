"use client"

import { getCoreRowModel, useReactTable, type ColumnDef, type SortingState } from "@tanstack/react-table"
import { PlusIcon, SearchIcon, XIcon } from "lucide-react"
import Link from "next/link"
import { useTranslations } from "next-intl"
import { useEffect, useMemo, useState } from "react"

import { DataTable } from "@/components/data-table/data-table"
import { DensityToggle } from "@/components/data-table/density-toggle"
import { Pagination } from "@/components/data-table/pagination"
import { FormAlert } from "@/components/forms/form-alert"
import { PageHeader } from "@/components/page-header"
import { useSession } from "@/components/shell/session-context"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { useErrorText } from "@/lib/api/errors"
import { useDepartments, useDesignations, useEmployeeList, useWorkLocations } from "@/lib/api/queries"
import { EmploymentTypes, type EmployeeSortField, type EmployeeSummary } from "@/lib/api/types"
import { formatDate } from "@/lib/dates"
import { Permissions } from "@/lib/permissions"
import {
  EMPLOYEE_PAGE_SIZES,
  employeeListParamsSchema,
  toEmployeeListQuery,
  type EmployeeListParams,
} from "@/lib/schemas/organization"
import { useUrlState } from "@/lib/use-url-state"

import { ActiveBadge } from "./active-badge"

/** Table column id to the API's sort field, for the sortable columns. */
const SORT_FIELDS: Record<string, EmployeeSortField> = {
  employeeCode: "EmployeeCode",
  name: "Name",
  department: "Department",
  designation: "Designation",
  joiningDate: "JoiningDate",
}
const COLUMN_FOR_SORT = Object.fromEntries(Object.entries(SORT_FIELDS).map(([column, field]) => [field, column]))

const ALL = "__all__"

/** Screen 26: the employee master, filtered, sorted and paged on the server, scoped to the caller (FR-8.3). */
export function EmployeeList() {
  const t = useTranslations("employees.list")
  const tTypes = useTranslations("employees.employmentTypes")
  const errorText = useErrorText()
  const { can } = useSession()
  const [params, setParams, defaults] = useUrlState(employeeListParamsSchema)

  const query = useMemo(() => toEmployeeListQuery(params), [params])
  const list = useEmployeeList(query)
  const departments = useDepartments()
  const designations = useDesignations()
  const locations = useWorkLocations()

  // Filters change the result set, so they go back to page 1.
  const setFilter = (patch: Partial<EmployeeListParams>, replace = false) => setParams({ ...patch, page: 1 }, { replace })

  const filtersActive =
    !!params.search ||
    !!params.departmentId ||
    !!params.designationId ||
    !!params.workLocationId ||
    !!params.employmentType ||
    params.status !== defaults.status

  const columns = useMemo<ColumnDef<EmployeeSummary>[]>(
    () => [
      {
        id: "employeeCode",
        // Sorting happens on the server; an accessor is what makes TanStack offer the sort control.
        accessorFn: (row) => row.employeeCode,
        header: t("columns.code"),
        meta: { label: t("columns.code"), className: "w-[120px]", skeletonWidth: "w-8" },
        cell: ({ row }) => (
          <Link href={`/employees/${row.original.publicId}`} className="font-mono text-ink hover:text-brand hover:underline">
            {row.original.employeeCode}
          </Link>
        ),
      },
      {
        id: "name",
        accessorFn: (row) => row.fullName,
        header: t("columns.name"),
        meta: { label: t("columns.name"), skeletonWidth: "w-18" },
        cell: ({ row }) => (
          <Link
            href={`/employees/${row.original.publicId}`}
            className="font-medium text-ink hover:text-brand hover:underline"
          >
            {row.original.fullName}
          </Link>
        ),
      },
      {
        id: "department",
        accessorFn: (row) => row.department.name,
        header: t("columns.department"),
        meta: { label: t("columns.department"), skeletonWidth: "w-14" },
        cell: ({ row }) => row.original.department.name,
      },
      {
        id: "designation",
        accessorFn: (row) => row.designation.name,
        header: t("columns.designation"),
        meta: { label: t("columns.designation"), skeletonWidth: "w-14" },
        cell: ({ row }) => row.original.designation.name,
      },
      {
        id: "workLocation",
        header: t("columns.location"),
        enableSorting: false,
        meta: { skeletonWidth: "w-6" },
        cell: ({ row }) => <span title={row.original.workLocation.name}>{row.original.workLocation.code}</span>,
      },
      {
        id: "employmentType",
        header: t("columns.type"),
        enableSorting: false,
        meta: { skeletonWidth: "w-9" },
        cell: ({ row }) => tTypes(row.original.employmentType),
      },
      {
        id: "manager",
        header: t("columns.manager"),
        enableSorting: false,
        meta: { skeletonWidth: "w-14" },
        cell: ({ row }) => row.original.reportingManager?.fullName ?? <span className="text-ink-muted">—</span>,
      },
      {
        id: "joiningDate",
        accessorFn: (row) => row.joiningDate,
        header: t("columns.joined"),
        meta: { label: t("columns.joined"), className: "w-[128px]", skeletonWidth: "w-10" },
        cell: ({ row }) => formatDate(row.original.joiningDate),
      },
      {
        id: "status",
        header: t("columns.status"),
        enableSorting: false,
        meta: { className: "w-[96px]", skeletonWidth: "w-7" },
        cell: ({ row }) => <ActiveBadge active={row.original.isActive} />,
      },
    ],
    [t, tTypes],
  )

  const sorting: SortingState = [{ id: COLUMN_FOR_SORT[params.sortBy], desc: params.sortDirection === "Desc" }]

  // The React Compiler isn't enabled; TanStack Table memoizes its own row models.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: list.data?.items ?? [],
    columns,
    getCoreRowModel: getCoreRowModel(),
    getRowId: (row) => row.publicId,
    manualSorting: true,
    manualPagination: true,
    enableSortingRemoval: false,
    pageCount: list.data?.totalPages ?? -1,
    state: { sorting, pagination: { pageIndex: params.page - 1, pageSize: params.pageSize } },
    onSortingChange: (updater) => {
      const next = typeof updater === "function" ? updater(sorting) : updater
      const first = next[0]
      if (!first) return
      setParams({ sortBy: SORT_FIELDS[first.id], sortDirection: first.desc ? "Desc" : "Asc", page: 1 })
    },
  })

  const total = list.data?.totalCount

  return (
    <div className="flex h-full flex-col">
      <PageHeader
        title={t("title")}
        description={total === undefined ? t("loadingCount") : t("count", { count: total })}
        actions={
          can(Permissions.Employee.Write) && (
            <Button asChild>
              <Link href="/employees/new">
                <PlusIcon aria-hidden="true" />
                {t("add")}
              </Link>
            </Button>
          )
        }
      />

      <div role="search" aria-label={t("filters.label")} className="flex flex-wrap items-center gap-1 border-b border-rule bg-surface px-3 py-1">
        <SearchBox value={params.search} onChange={(search) => setFilter({ search }, true)} />
        <FilterSelect
          label={t("filters.department")}
          allLabel={t("filters.allDepartments")}
          value={params.departmentId}
          onChange={(departmentId) => setFilter({ departmentId })}
          options={(departments.data ?? []).map((d) => ({ value: d.publicId, label: d.name }))}
        />
        <FilterSelect
          label={t("filters.designation")}
          allLabel={t("filters.allDesignations")}
          value={params.designationId}
          onChange={(designationId) => setFilter({ designationId })}
          options={(designations.data ?? []).map((d) => ({ value: d.publicId, label: d.name }))}
        />
        <FilterSelect
          label={t("filters.location")}
          allLabel={t("filters.allLocations")}
          value={params.workLocationId}
          onChange={(workLocationId) => setFilter({ workLocationId })}
          options={(locations.data ?? []).map((l) => ({ value: l.publicId, label: l.name }))}
        />
        <FilterSelect
          label={t("filters.type")}
          allLabel={t("filters.allTypes")}
          value={params.employmentType}
          onChange={(employmentType) => setFilter({ employmentType: employmentType as EmployeeListParams["employmentType"] })}
          options={EmploymentTypes.map((type) => ({ value: type, label: tTypes(type) }))}
        />
        <Select
          value={params.status}
          onValueChange={(status) => setFilter({ status: status as EmployeeListParams["status"] })}
        >
          <SelectTrigger aria-label={t("filters.status")} className="min-w-14">
            <SelectValue>{t(`filters.status${params.status === "active" ? "Active" : params.status === "inactive" ? "Inactive" : "All"}`)}</SelectValue>
          </SelectTrigger>
          <SelectContent position="popper">
            <SelectItem value="active">{t("filters.statusActive")}</SelectItem>
            <SelectItem value="inactive">{t("filters.statusInactive")}</SelectItem>
            <SelectItem value="all">{t("filters.statusAll")}</SelectItem>
          </SelectContent>
        </Select>
        {filtersActive && (
          <Button
            variant="ghost"
            onClick={() =>
              setParams({
                search: "",
                departmentId: undefined,
                designationId: undefined,
                workLocationId: undefined,
                employmentType: undefined,
                status: defaults.status,
                page: 1,
              })
            }
          >
            <XIcon aria-hidden="true" />
            {t("filters.clear")}
          </Button>
        )}
        <div className="ml-auto">
          <DensityToggle />
        </div>
      </div>

      {list.isError && !list.data ? (
        <div className="p-3">
          <FormAlert>
            <p>{errorText(list.error)}</p>
            <Button variant="outline" size="sm" className="mt-1" onClick={() => void list.refetch()}>
              {t("retry")}
            </Button>
          </FormAlert>
        </div>
      ) : (
        <DataTable
          table={table}
          caption={t("caption")}
          isLoading={list.isPending}
          isRefreshing={list.isPlaceholderData}
          skeletonRows={params.pageSize}
          className="min-h-0 flex-1 bg-surface"
          empty={
            filtersActive ? (
              <p className="text-14 text-ink-muted">{t("emptyFiltered")}</p>
            ) : (
              <div className="flex flex-col items-center gap-1">
                <p className="text-14 text-ink-muted">{t("empty")}</p>
                {can(Permissions.Employee.Write) && (
                  <Button asChild variant="outline">
                    <Link href="/employees/new">{t("add")}</Link>
                  </Button>
                )}
              </div>
            )
          }
        />
      )}

      {list.data && (
        <Pagination
          page={list.data.page}
          pageSize={list.data.pageSize}
          totalCount={list.data.totalCount}
          totalPages={list.data.totalPages}
          pageSizes={EMPLOYEE_PAGE_SIZES}
          onPageChange={(page) => setParams({ page })}
          onPageSizeChange={(pageSize) => setParams({ pageSize, page: 1 })}
        />
      )}
    </div>
  )
}

/** Search by code or name. Typing updates the URL after a pause, replacing rather than adding history. */
function SearchBox({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const t = useTranslations("employees.list.filters")
  const [text, setText] = useState(value)

  // Follow the URL when it changes from elsewhere (back button, "Clear filters").
  // Keeps what was typed (trailing space included) when the URL merely caught up with it.
  // eslint-disable-next-line react-hooks/set-state-in-effect -- mirrors external state into the input
  useEffect(() => setText((current) => (current.trim() === value ? current : value)), [value])

  useEffect(() => {
    if (text.trim() === value) return
    const timer = setTimeout(() => onChange(text.trim()), 300)
    return () => clearTimeout(timer)
  }, [text, value, onChange])

  return (
    <div className="relative w-full max-w-[280px]">
      <SearchIcon aria-hidden="true" className="pointer-events-none absolute top-1/2 left-1 size-2 -translate-y-1/2 text-ink-muted" />
      <Input
        type="search"
        value={text}
        onChange={(event) => setText(event.target.value)}
        placeholder={t("searchPlaceholder")}
        aria-label={t("search")}
        maxLength={100}
        className="pl-4"
      />
    </div>
  )
}

function FilterSelect({
  label,
  allLabel,
  value,
  onChange,
  options,
}: {
  label: string
  allLabel: string
  value: string | undefined
  onChange: (value: string | undefined) => void
  options: { value: string; label: string }[]
}) {
  return (
    <Select value={value ?? ALL} onValueChange={(next) => onChange(next === ALL ? undefined : next)}>
      <SelectTrigger aria-label={label} className="max-w-24 min-w-14">
        {/* Explicit text: Radix only fills SelectValue in after its items mount, which leaves a blank trigger. */}
        <SelectValue>{options.find((option) => option.value === value)?.label ?? allLabel}</SelectValue>
      </SelectTrigger>
      <SelectContent position="popper">
        <SelectItem value={ALL}>{allLabel}</SelectItem>
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

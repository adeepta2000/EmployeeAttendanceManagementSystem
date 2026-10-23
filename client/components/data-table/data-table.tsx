"use client"

import { flexRender, type RowData, type Table as TanStackTable } from "@tanstack/react-table"
import { ArrowDownIcon, ArrowUpDownIcon, ArrowUpIcon } from "lucide-react"
import { useTranslations } from "next-intl"
import type { ReactNode } from "react"

import { useDensity } from "@/components/shell/density"
import { Skeleton } from "@/components/ui/skeleton"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { cn } from "@/lib/utils"

declare module "@tanstack/react-table" {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- the generics must match TanStack's declaration
  interface ColumnMeta<TData extends RowData, TValue> {
    /** Classes for this column's header and body cells (width, alignment). */
    className?: string
    /** Width of the placeholder bar while loading, so the skeleton has the real table's shape. */
    skeletonWidth?: string
    /** The visible header text, for the sort button's accessible name when the header is a node. */
    label?: string
  }
}

type Props<T> = {
  table: TanStackTable<T>
  /** True only for the first load; later fetches keep the previous rows on screen. */
  isLoading: boolean
  /** How many placeholder rows to draw while loading: the page size, so nothing shifts when data lands. */
  skeletonRows: number
  /** What to show when there are no rows: say what to do next, not just "no data" (§4.5). */
  empty: ReactNode
  caption: string
  /** Dims the rows while a new page or filter loads behind the current one. */
  isRefreshing?: boolean
  className?: string
}

/**
 * A server-driven table (manual sorting and paging). Rows are 40px, or 32px in compact density (§4.2);
 * the header sticks while the body scrolls; headers have scope and aria-sort (§4.7).
 */
export function DataTable<T>({ table, isLoading, skeletonRows, empty, caption, isRefreshing, className }: Props<T>) {
  const t = useTranslations("table")
  const { density } = useDensity()
  const columns = table.getVisibleLeafColumns()
  const rows = table.getRowModel().rows

  return (
    <Table
      data-density={density}
      aria-busy={isLoading || isRefreshing || undefined}
      containerClassName={cn("overflow-auto", className)}
      className="border-separate border-spacing-0"
    >
      <caption className="sr-only">{caption}</caption>
      <TableHeader className="sticky top-0 z-10 bg-surface [&_th]:border-b [&_th]:border-rule">
        {table.getHeaderGroups().map((group) => (
          <TableRow key={group.id} className="border-0 hover:bg-surface">
            {group.headers.map((header) => {
              const sorted = header.column.getIsSorted()
              const canSort = header.column.getCanSort()
              const label = header.column.columnDef.meta?.label
              return (
                <TableHead
                  key={header.id}
                  scope="col"
                  aria-sort={sorted === "asc" ? "ascending" : sorted === "desc" ? "descending" : canSort ? "none" : undefined}
                  className={header.column.columnDef.meta?.className}
                >
                  {header.isPlaceholder ? null : canSort ? (
                    <button
                      type="button"
                      onClick={header.column.getToggleSortingHandler()}
                      className="-mx-0.5 inline-flex items-center gap-0.5 rounded-sm px-0.5 hover:text-ink"
                      aria-label={label ? t("sortBy", { column: label }) : undefined}
                    >
                      {flexRender(header.column.columnDef.header, header.getContext())}
                      {sorted === "asc" ? (
                        <ArrowUpIcon aria-hidden="true" className="size-1.5 text-ink" />
                      ) : sorted === "desc" ? (
                        <ArrowDownIcon aria-hidden="true" className="size-1.5 text-ink" />
                      ) : (
                        <ArrowUpDownIcon aria-hidden="true" className="size-1.5 opacity-50" />
                      )}
                    </button>
                  ) : (
                    flexRender(header.column.columnDef.header, header.getContext())
                  )}
                </TableHead>
              )
            })}
          </TableRow>
        ))}
      </TableHeader>

      <TableBody className={cn("transition-opacity [&_td]:border-b [&_td]:border-rule", isRefreshing && "opacity-60")}>
        {isLoading ? (
          Array.from({ length: skeletonRows }, (_, index) => (
            <TableRow key={index} aria-hidden="true" className="border-0 hover:bg-surface">
              {columns.map((column) => (
                <TableCell key={column.id} className={column.columnDef.meta?.className}>
                  <Skeleton className={cn("h-1.5", column.columnDef.meta?.skeletonWidth ?? "w-[70%]")} />
                </TableCell>
              ))}
            </TableRow>
          ))
        ) : rows.length === 0 ? (
          <TableRow className="border-0 hover:bg-surface">
            <TableCell colSpan={columns.length} className="h-auto py-6 text-center whitespace-normal">
              {empty}
            </TableCell>
          </TableRow>
        ) : (
          rows.map((row) => (
            <TableRow key={row.id} className="border-0">
              {row.getVisibleCells().map((cell) => (
                <TableCell key={cell.id} className={cell.column.columnDef.meta?.className}>
                  {flexRender(cell.column.columnDef.cell, cell.getContext())}
                </TableCell>
              ))}
            </TableRow>
          ))
        )}
      </TableBody>
    </Table>
  )
}

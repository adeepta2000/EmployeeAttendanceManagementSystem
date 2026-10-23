"use client"

import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react"
import { useTranslations } from "next-intl"

import { Button } from "@/components/ui/button"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"

type Props = {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  pageSizes: readonly number[]
  onPageChange: (page: number) => void
  onPageSizeChange: (size: number) => void
}

export function Pagination({ page, pageSize, totalCount, totalPages, pageSizes, onPageChange, onPageSizeChange }: Props) {
  const t = useTranslations("table.pagination")
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)

  return (
    <nav
      aria-label={t("label")}
      className="flex flex-wrap items-center justify-between gap-2 border-t border-rule bg-surface px-3 py-1 text-13 text-ink-muted"
    >
      <p className="tabular" aria-live="polite">
        {t("showing", { from, to, total: totalCount })}
      </p>
      <div className="flex items-center gap-2">
        <label className="flex items-center gap-1">
          <span>{t("rowsPerPage")}</span>
          <Select value={String(pageSize)} onValueChange={(value) => onPageSizeChange(Number(value))}>
            <SelectTrigger size="sm" className="tabular" aria-label={t("rowsPerPage")}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent position="popper">
              {pageSizes.map((size) => (
                <SelectItem key={size} value={String(size)} className="tabular">
                  {size}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </label>
        <span className="tabular">{t("pageOf", { page: totalPages === 0 ? 0 : page, pages: totalPages })}</span>
        <div className="flex items-center gap-0.5">
          <Button
            variant="outline"
            size="icon-sm"
            onClick={() => onPageChange(page - 1)}
            disabled={page <= 1}
            aria-label={t("previous")}
          >
            <ChevronLeftIcon aria-hidden="true" />
          </Button>
          <Button
            variant="outline"
            size="icon-sm"
            onClick={() => onPageChange(page + 1)}
            disabled={page >= totalPages}
            aria-label={t("next")}
          >
            <ChevronRightIcon aria-hidden="true" />
          </Button>
        </div>
      </div>
    </nav>
  )
}

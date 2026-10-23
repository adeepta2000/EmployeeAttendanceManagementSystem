"use client"

import { PlusIcon, SearchIcon } from "lucide-react"
import Link from "next/link"
import { usePathname } from "next/navigation"
import { useTranslations } from "next-intl"
import { useMemo, useState, type ReactNode } from "react"
import { z } from "zod"

import { DensityToggle } from "@/components/data-table/density-toggle"
import { FormAlert } from "@/components/forms/form-alert"
import { PageHeader } from "@/components/page-header"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Spinner } from "@/components/ui/spinner"
import { useErrorText } from "@/lib/api/errors"
import { useUrlState } from "@/lib/use-url-state"
import { cn } from "@/lib/utils"

const TABS = [
  { href: "/departments", key: "departments" },
  { href: "/designations", key: "designations" },
] as const

/** Screen 28's header: one screen, two lists, each with its own URL so either can be bookmarked. */
export function OrgStructureHeader() {
  const t = useTranslations("org")
  const pathname = usePathname()
  return (
    <PageHeader title={t("title")} description={t("description")}>
      <nav aria-label={t("title")} className="-mb-px flex gap-2">
        {TABS.map((tab) => {
          const active = pathname.startsWith(tab.href)
          return (
            <Link
              key={tab.href}
              href={tab.href}
              aria-current={active ? "page" : undefined}
              className={cn(
                "flex h-5 items-center border-b-2 px-0.5 text-14 font-medium",
                active ? "border-brand text-brand" : "border-transparent text-ink-muted hover:text-ink",
              )}
            >
              {t(`tabs.${tab.key}`)}
            </Link>
          )
        })}
      </nav>
    </PageHeader>
  )
}

export const masterDataParamsSchema = z.object({
  q: z.string().trim().max(100).catch("").default(""),
  status: z.enum(["active", "retired", "all"]).catch("active").default("active"),
})

/**
 * Search and status filter for a short master-data list. The lists are small and fully loaded, so the
 * filtering happens here, instantly; the filter still lives in the URL (§4.5).
 */
export function useMasterDataFilter<T extends { code: string; name: string; isActive: boolean }>(
  rows: T[] | undefined,
  extraText: (row: T) => string = () => "",
) {
  const [params, setParams] = useUrlState(masterDataParamsSchema)
  const filtered = useMemo(() => {
    const term = params.q.toLowerCase()
    return (rows ?? []).filter(
      (row) =>
        (params.status === "all" || row.isActive === (params.status === "active")) &&
        (!term || `${row.code} ${row.name} ${extraText(row)}`.toLowerCase().includes(term)),
    )
  }, [rows, params, extraText])
  return { params, setParams, filtered }
}

export function MasterDataToolbar({
  params,
  setParams,
  addLabel,
  onAdd,
}: {
  params: z.output<typeof masterDataParamsSchema>
  setParams: (patch: Partial<z.output<typeof masterDataParamsSchema>>, options?: { replace?: boolean }) => void
  /** Absent when the user can't add: the button isn't shown. */
  addLabel?: string
  onAdd?: () => void
}) {
  const t = useTranslations("org.toolbar")
  return (
    <div role="search" aria-label={t("label")} className="flex flex-wrap items-center gap-1 border-b border-rule bg-surface px-3 py-1">
      <div className="relative w-full max-w-[280px]">
        <SearchIcon aria-hidden="true" className="pointer-events-none absolute top-1/2 left-1 size-2 -translate-y-1/2 text-ink-muted" />
        <Input
          type="search"
          value={params.q}
          onChange={(event) => setParams({ q: event.target.value }, { replace: true })}
          placeholder={t("searchPlaceholder")}
          aria-label={t("search")}
          maxLength={100}
          className="pl-4"
        />
      </div>
      <Select value={params.status} onValueChange={(status) => setParams({ status: status as typeof params.status })}>
        <SelectTrigger aria-label={t("status")} className="min-w-14">
          <SelectValue>{t(params.status === "active" ? "statusActive" : params.status === "retired" ? "statusRetired" : "statusAll")}</SelectValue>
        </SelectTrigger>
        <SelectContent position="popper">
          <SelectItem value="active">{t("statusActive")}</SelectItem>
          <SelectItem value="retired">{t("statusRetired")}</SelectItem>
          <SelectItem value="all">{t("statusAll")}</SelectItem>
        </SelectContent>
      </Select>
      <div className="ml-auto flex items-center gap-1">
        <DensityToggle />
        {addLabel && onAdd && (
          <Button onClick={onAdd}>
            <PlusIcon aria-hidden="true" />
            {addLabel}
          </Button>
        )}
      </div>
    </div>
  )
}

/**
 * refuses it while the record is still in use and says what to move first, which this dialog shows.
 */
export function SetActiveDialog({
  open,
  onOpenChange,
  retiring,
  title,
  description,
  confirmLabel,
  onConfirm,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  retiring: boolean
  title: string
  description: ReactNode
  confirmLabel: string
  onConfirm: () => Promise<unknown>
}) {
  const t = useTranslations("common")
  const errorText = useErrorText()
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) setError(null)
        onOpenChange(next)
      }}
    >
      <DialogContent className="sm:max-w-[440px]">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <FormAlert>{error}</FormAlert>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            {t("cancel")}
          </Button>
          <Button
            type="button"
            variant={retiring ? "destructive" : "default"}
            disabled={pending}
            onClick={async () => {
              setPending(true)
              setError(null)
              try {
                await onConfirm()
                onOpenChange(false)
              } catch (failure) {
                setError(errorText(failure))
              } finally {
                setPending(false)
              }
            }}
          >
            {pending && <Spinner />}
            {confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** The list's error and empty states. */
export function ListMessage({ children }: { children: ReactNode }) {
  return <div className="p-3">{children}</div>
}

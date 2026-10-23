"use client"

import { useRouter } from "next/navigation"
import { useTranslations } from "next-intl"
import { useDeferredValue, useState } from "react"

import {
  Command,
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command"
import { Spinner } from "@/components/ui/spinner"
import { useEmployeeSearch } from "@/lib/api/queries"
import type { NavGroup } from "@/lib/nav"

import { navIcons } from "./nav-icons"

type Props = {
  open: boolean
  onOpenChange: (open: boolean) => void
  groups: NavGroup[]
  /** Whether the user can look up employees beyond themselves. */
  canSearchEmployees: boolean
}

/**
 * The global search (§4.3, Ctrl/Cmd+K): employees by code or name, plus jump-to-screen commands for the
 * screens in the user's menu. Employee results come from the API, already limited to the caller's scope.
 */
export function CommandPalette({ open, onOpenChange, groups, canSearchEmployees }: Props) {
  const t = useTranslations("shell.search")
  const tNav = useTranslations("nav.items")
  const router = useRouter()
  const [term, setTerm] = useState("")
  const deferredTerm = useDeferredValue(term.trim())

  const employees = useEmployeeSearch(deferredTerm, {
    enabled: open && canSearchEmployees && deferredTerm.length >= 2,
    pageSize: 6,
  })

  const screens = groups
    .flatMap((group) => group.items)
    .filter((item) => item.available)
    .filter((item) => tNav(item.id).toLowerCase().includes(term.trim().toLowerCase()))

  const go = (href: string) => {
    onOpenChange(false)
    setTerm("")
    router.push(href)
  }

  const showEmployees = canSearchEmployees && deferredTerm.length >= 2
  const employeeResults = showEmployees ? (employees.data?.items ?? []) : []

  return (
    <CommandDialog
      open={open}
      onOpenChange={(next) => {
        onOpenChange(next)
        if (!next) setTerm("")
      }}
      title={t("title")}
      description={t("description")}
    >
      {/* Results are filtered here and by the API, not by cmdk's fuzzy matcher. */}
      <Command shouldFilter={false}>
        <CommandInput value={term} onValueChange={setTerm} placeholder={t("placeholder")} />
        <CommandList>
          <CommandEmpty>
            {employees.isFetching ? <Spinner className="mx-auto" /> : t(term.trim().length < 2 ? "hint" : "empty")}
          </CommandEmpty>

          {employeeResults.length > 0 && (
            <CommandGroup heading={t("employees")}>
              {employeeResults.map((employee) => (
                <CommandItem
                  key={employee.publicId}
                  value={`employee-${employee.publicId}`}
                  onSelect={() => go(`/employees/${employee.publicId}`)}
                >
                  <span className="w-9 shrink-0 font-mono text-13 text-ink-muted">{employee.employeeCode}</span>
                  <span className="min-w-0 flex-1 truncate">{employee.fullName}</span>
                  <span className="truncate text-12 text-ink-muted">{employee.department.name}</span>
                </CommandItem>
              ))}
            </CommandGroup>
          )}

          {screens.length > 0 && (
            <CommandGroup heading={t("screens")}>
              {screens.map((item) => {
                const Icon = navIcons[item.id]
                return (
                  <CommandItem key={item.id} value={`screen-${item.id}`} onSelect={() => go(item.href)}>
                    {Icon && <Icon aria-hidden="true" />}
                    {tNav(item.id)}
                  </CommandItem>
                )
              })}
            </CommandGroup>
          )}
        </CommandList>
      </Command>
    </CommandDialog>
  )
}

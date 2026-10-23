"use client"

import { ChevronsUpDownIcon, XIcon } from "lucide-react"
import { useTranslations } from "next-intl"
import { useDeferredValue, useState } from "react"

import { Button } from "@/components/ui/button"
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command"
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover"
import { Spinner } from "@/components/ui/spinner"
import { useEmployeeSearch } from "@/lib/api/queries"
import type { EmployeeRef } from "@/lib/api/types"
import { cn } from "@/lib/utils"

type Props = {
  /** The chosen employee's PublicId, or "" for none. */
  value: string
  onChange: (value: string) => void
  onBlur?: () => void
  /** Shows the current choice before any search has run (it comes with the record being edited). */
  initial?: EmployeeRef | null
  /** Records that can't be picked, e.g. the employee being edited as their own manager. */
  excludeIds?: string[]
  placeholder: string
  id?: string
  "aria-invalid"?: boolean
  "aria-describedby"?: string
  "aria-required"?: boolean
}

/**
 * Picks an active employee by code or name, e.g. a reporting manager or a department head. The search
 * runs on the API, so it only ever offers people inside the caller's scope (FR-8.3).
 */
export function EmployeePicker({ value, onChange, onBlur, initial, excludeIds = [], placeholder, ...control }: Props) {
  const t = useTranslations("employees.picker")
  const [open, setOpen] = useState(false)
  const [term, setTerm] = useState("")
  const deferredTerm = useDeferredValue(term)
  const [selected, setSelected] = useState<EmployeeRef | null>(initial && initial.publicId === value ? initial : null)

  const search = useEmployeeSearch(deferredTerm, { enabled: open, pageSize: 10 })
  const results = (search.data?.items ?? []).filter((employee) => !excludeIds.includes(employee.publicId))
  const current = value && selected?.publicId === value ? selected : null

  return (
    <div className="flex items-center gap-0.5">
      <Popover
        open={open}
        onOpenChange={(next) => {
          setOpen(next)
          if (!next) onBlur?.()
        }}
      >
        <PopoverTrigger asChild>
          <Button
            type="button"
            variant="outline"
            role="combobox"
            aria-expanded={open}
            className={cn("h-4 min-w-0 flex-1 justify-between px-1.25 font-normal", !current && "text-ink-muted")}
            {...control}
          >
            <span className="min-w-0 truncate">
              {current ? (
                <>
                  <span className="font-mono text-13 text-ink-muted">{current.employeeCode}</span>{" "}
                  <span className="text-ink">{current.fullName}</span>
                </>
              ) : (
                placeholder
              )}
            </span>
            <ChevronsUpDownIcon aria-hidden="true" className="opacity-50" />
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-(--radix-popover-trigger-width) min-w-36 p-0">
          <Command shouldFilter={false}>
            <CommandInput value={term} onValueChange={setTerm} placeholder={t("searchPlaceholder")} />
            <CommandList>
              <CommandEmpty>{search.isFetching ? <Spinner className="mx-auto" /> : t("empty")}</CommandEmpty>
              {results.length > 0 && (
                <CommandGroup>
                  {results.map((employee) => (
                    <CommandItem
                      key={employee.publicId}
                      value={employee.publicId}
                      onSelect={() => {
                        setSelected({
                          publicId: employee.publicId,
                          employeeCode: employee.employeeCode,
                          fullName: employee.fullName,
                        })
                        onChange(employee.publicId)
                        setOpen(false)
                        setTerm("")
                      }}
                    >
                      <span className="w-9 shrink-0 font-mono text-13 text-ink-muted">{employee.employeeCode}</span>
                      <span className="min-w-0 flex-1 truncate">{employee.fullName}</span>
                      <span className="truncate text-12 text-ink-muted">{employee.designation.name}</span>
                    </CommandItem>
                  ))}
                </CommandGroup>
              )}
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
      {current && (
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label={t("clear", { name: current.fullName })}
          onClick={() => {
            setSelected(null)
            onChange("")
          }}
        >
          <XIcon aria-hidden="true" />
        </Button>
      )}
    </div>
  )
}

"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { format, getDay, getDaysInMonth, parseISO } from "date-fns"
import { ChevronLeftIcon, ChevronRightIcon, MapPinIcon, PencilIcon, PlusIcon, Trash2Icon } from "lucide-react"
import { useTranslations } from "next-intl"
import { useMemo, useState } from "react"
import { Controller, useForm, useWatch } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { applyApiErrors } from "@/components/forms/api-errors"
import { CustomField, TextField } from "@/components/forms/fields"
import { FormAlert } from "@/components/forms/form-alert"
import { PageHeader } from "@/components/page-header"
import { useSession } from "@/components/shell/session-context"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import { Spinner } from "@/components/ui/spinner"
import { Textarea } from "@/components/ui/textarea"
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip"
import { useErrorText, useMessageText } from "@/lib/api/errors"
import { useWorkLocations } from "@/lib/api/queries"
import { useHolidayMutations, useHolidays } from "@/lib/api/scheduling-queries"
import { DaysOfWeek, type Holiday } from "@/lib/api/scheduling-types"
import { todayInBusinessZone } from "@/lib/dates"
import { Permissions } from "@/lib/permissions"
import { holidaySchema, type HolidayInput, type HolidayOutput } from "@/lib/schemas/scheduling"
import { AttendanceStatuses } from "@/lib/status"
import { useUrlState } from "@/lib/use-url-state"
import { cn } from "@/lib/utils"

const holidayStyle = AttendanceStatuses.holiday
const MONTHS = Array.from({ length: 12 }, (_, index) => index)

const thisYear = () => Number(todayInBusinessZone().slice(0, 4))

/** The year and location shown, in the URL (§4.5), so "Chittagong 2027" can be bookmarked. */
const paramsSchema = z.object({
  year: z.coerce.number().int().min(2000).max(2099).optional().catch(undefined),
  location: z.guid().optional().catch(undefined),
})

type DialogState = { kind: "edit"; holiday?: Holiday; date?: string } | { kind: "delete"; holiday: Holiday } | null

/**
 * Screen 32 (FR-7.1): a year of holidays at a glance, the same holidays as a list beside it, and add,
 * edit and remove with the locations each one applies to. A holiday drives the day type and the OT
 * multiplier (FR-7.2), so it is marked with the holiday status colour and its letter code.
 */
export function HolidayCalendar() {
  const t = useTranslations("scheduling.holidays")
  const errorText = useErrorText()
  const { can } = useSession()
  const canWrite = can(Permissions.Holiday.Write)
  const [params, setParams] = useUrlState(paramsSchema)
  const year = params.year ?? thisYear()
  const holidays = useHolidays(year, params.location)
  const locations = useWorkLocations()
  const [dialog, setDialog] = useState<DialogState>(null)

  const byDate = useMemo(() => {
    const map = new Map<string, Holiday[]>()
    for (const holiday of holidays.data ?? []) map.set(holiday.holidayDate, [...(map.get(holiday.holidayDate) ?? []), holiday])
    return map
  }, [holidays.data])

  const list = holidays.data ?? []
  const optionalCount = list.filter((holiday) => holiday.isOptional).length
  const today = todayInBusinessZone()
  const location = locations.data?.find((l) => l.publicId === params.location)

  const openDate = (date: string) => {
    const existing = byDate.get(date)
    if (existing?.length) setDialog({ kind: "edit", holiday: existing[0] })
    else if (canWrite) setDialog({ kind: "edit", date })
  }

  return (
    <div className="flex h-full flex-col">
      <PageHeader
        title={t("title")}
        description={t("description")}
        actions={
          canWrite && (
            <Button onClick={() => setDialog({ kind: "edit", date: year === thisYear() ? today : `${year}-01-01` })}>
              <PlusIcon aria-hidden="true" />
              {t("add")}
            </Button>
          )
        }
      />

      <div role="search" aria-label={t("filters")} className="flex flex-wrap items-center gap-1 border-b border-rule bg-surface px-3 py-1">
        <div className="flex items-center gap-0.5">
          <Button
            variant="outline"
            size="icon-sm"
            onClick={() => setParams({ year: year - 1 })}
            disabled={year <= 2000}
            aria-label={t("previousYear")}
          >
            <ChevronLeftIcon aria-hidden="true" />
          </Button>
          <h2 className="w-6 text-center font-mono text-16 font-semibold tabular" aria-live="polite">
            {year}
          </h2>
          <Button
            variant="outline"
            size="icon-sm"
            onClick={() => setParams({ year: year + 1 })}
            disabled={year >= 2099}
            aria-label={t("nextYear")}
          >
            <ChevronRightIcon aria-hidden="true" />
          </Button>
          {year !== thisYear() && (
            <Button variant="ghost" size="sm" onClick={() => setParams({ year: undefined })}>
              {t("thisYear")}
            </Button>
          )}
        </div>
        <Select value={params.location ?? "all"} onValueChange={(value) => setParams({ location: value === "all" ? undefined : value })}>
          <SelectTrigger aria-label={t("location")} className="min-w-18">
            <MapPinIcon aria-hidden="true" className="text-ink-muted" />
            <SelectValue>{location?.name ?? t("allLocations")}</SelectValue>
          </SelectTrigger>
          <SelectContent position="popper">
            <SelectItem value="all">{t("allLocations")}</SelectItem>
            {(locations.data ?? []).map((l) => (
              <SelectItem key={l.publicId} value={l.publicId}>
                {l.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <p className="ml-auto text-13 text-ink-muted tabular" aria-live="polite">
          {holidays.isPending ? t("loading") : t("count", { count: list.length, optional: optionalCount })}
        </p>
      </div>

      {holidays.isError ? (
        <div className="p-3">
          <FormAlert>{errorText(holidays.error)}</FormAlert>
        </div>
      ) : (
        <div className="flex min-h-0 flex-1">
          <div className={cn("relative min-w-0 flex-1 overflow-auto p-3 transition-opacity", holidays.isPlaceholderData && "opacity-60")}>
            <div className="grid grid-cols-[repeat(auto-fill,minmax(216px,1fr))] gap-2">
              {MONTHS.map((month) => (
                <MonthCard
                  key={month}
                  year={year}
                  month={month}
                  byDate={byDate}
                  today={today}
                  loading={holidays.isPending}
                  canWrite={canWrite}
                  onOpen={openDate}
                />
              ))}
            </div>
            <Legend filtered={!!params.location} />
          </div>

          <aside aria-label={t("listLabel", { year })} className="flex w-[360px] shrink-0 flex-col border-l border-rule bg-surface">
            <h2 className="border-b border-rule px-1.5 py-1 text-14 font-semibold">{t("listLabel", { year })}</h2>
            <div className="relative min-h-0 flex-1 overflow-auto">
              {holidays.isPending ? (
                <ul aria-hidden="true">
                  {Array.from({ length: 6 }, (_, index) => (
                    <li key={index} className="flex gap-1 border-b border-rule px-1.5 py-1">
                      <Skeleton className="h-4 w-5" />
                      <Skeleton className="h-2 flex-1" />
                    </li>
                  ))}
                </ul>
              ) : list.length === 0 ? (
                <div className="flex flex-col items-start gap-1 p-1.5 text-14 text-ink-muted">
                  <p>{t(params.location ? "emptyLocation" : "empty", { year })}</p>
                  {canWrite && (
                    <Button variant="outline" size="sm" onClick={() => setDialog({ kind: "edit", date: `${year}-01-01` })}>
                      <PlusIcon aria-hidden="true" />
                      {t("add")}
                    </Button>
                  )}
                </div>
              ) : (
                <ul>
                  {list.map((holiday) => (
                    <HolidayRow
                      key={holiday.publicId}
                      holiday={holiday}
                      canWrite={canWrite}
                      onEdit={() => setDialog({ kind: "edit", holiday })}
                      onDelete={() => setDialog({ kind: "delete", holiday })}
                    />
                  ))}
                </ul>
              )}
            </div>
          </aside>
        </div>
      )}

      {dialog?.kind === "edit" && (
        <HolidayDialog
          holiday={dialog.holiday}
          date={dialog.date}
          readOnly={!canWrite}
          onClose={() => setDialog(null)}
          onDelete={dialog.holiday && canWrite ? () => setDialog({ kind: "delete", holiday: dialog.holiday! }) : undefined}
        />
      )}
      {dialog?.kind === "delete" && <DeleteHolidayDialog holiday={dialog.holiday} onClose={() => setDialog(null)} />}
    </div>
  )
}

function MonthCard({
  year,
  month,
  byDate,
  today,
  loading,
  canWrite,
  onOpen,
}: {
  year: number
  month: number
  byDate: Map<string, Holiday[]>
  today: string
  loading: boolean
  canWrite: boolean
  onOpen: (date: string) => void
}) {
  const t = useTranslations("scheduling.holidays")
  const tDays = useTranslations("scheduling.days")
  const first = new Date(year, month, 1)
  const days = getDaysInMonth(first)
  const monthName = format(first, "MMMM")

  return (
    <section aria-label={`${monthName} ${year}`} className="rounded-md border border-rule bg-surface p-1">
      <h3 className="mb-0.5 text-13 font-semibold">{monthName}</h3>
      <div className="grid grid-cols-7 gap-0.25 text-center text-12">
        {DaysOfWeek.map((day) => (
          <abbr key={day} title={tDays(`long.${day}`)} className="text-ink-muted no-underline">
            {tDays(`initial.${day}`)}
          </abbr>
        ))}
        {Array.from({ length: getDay(first) }, (_, blank) => (
          <span key={`blank-${blank}`} />
        ))}
        {Array.from({ length: days }, (_, index) => {
          const day = index + 1
          const date = `${year}-${String(month + 1).padStart(2, "0")}-${String(day).padStart(2, "0")}`
          const holidays = byDate.get(date) ?? []
          const counted = holidays.filter((h) => !h.isOptional)
          const limited = holidays.length > 0 && holidays.every((h) => h.workLocations.length > 0)
          const label =
            holidays.length > 0
              ? t("cellLabel", { date: format(new Date(year, month, day), "d MMMM"), names: holidays.map((h) => h.name).join(", ") })
              : t("cellEmpty", { date: format(new Date(year, month, day), "d MMMM") })
          const cell = (
            <button
              type="button"
              disabled={loading || (holidays.length === 0 && !canWrite)}
              onClick={() => onOpen(date)}
              aria-label={label}
              className={cn(
                "relative flex h-3 items-center justify-center rounded-sm border font-mono text-12 transition-colors",
                counted.length > 0
                  ? cn(holidayStyle.tint, holidayStyle.border, holidayStyle.text, "font-semibold")
                  : holidays.length > 0
                    ? cn("border-dashed", holidayStyle.border, holidayStyle.text)
                    : "border-transparent text-ink enabled:hover:border-brand enabled:hover:bg-brand-soft",
                date === today && "ring-2 ring-brand ring-inset",
                "disabled:cursor-default",
              )}
            >
              {day}
              {limited && <span aria-hidden="true" className={cn("absolute bottom-0.25 size-0.5 rounded-full", "bg-holiday")} />}
            </button>
          )
          return holidays.length > 0 ? (
            <Tooltip key={day}>
              <TooltipTrigger asChild>{cell}</TooltipTrigger>
              <TooltipContent className="flex-col items-start">
                {holidays.map((holiday) => (
                  <span key={holiday.publicId}>
                    {holiday.name}
                    {holiday.isOptional && ` · ${t("optional")}`}
                    {holiday.workLocations.length > 0 && ` · ${holiday.workLocations.map((l) => l.code).join(", ")}`}
                  </span>
                ))}
              </TooltipContent>
            </Tooltip>
          ) : (
            <span key={day} className="contents">
              {cell}
            </span>
          )
        })}
      </div>
    </section>
  )
}

function Legend({ filtered }: { filtered: boolean }) {
  const t = useTranslations("scheduling.holidays.legend")
  return (
    <ul className="mt-2 flex flex-wrap gap-x-2 gap-y-0.5 text-12 text-ink-muted" aria-label={t("label")}>
      <li className="flex items-center gap-0.5">
        <span className={cn("flex size-2.5 items-center justify-center rounded-sm border font-semibold", holidayStyle.tint, holidayStyle.border, holidayStyle.text)}>
          {holidayStyle.code}
        </span>
        {t("holiday")}
      </li>
      {!filtered && (
        <li className="flex items-center gap-0.5">
          <span className={cn("relative size-2.5 rounded-sm border", holidayStyle.tint, holidayStyle.border)}>
            <span className="absolute bottom-0.25 left-1/2 size-0.5 -translate-x-1/2 rounded-full bg-holiday" />
          </span>
          {t("limited")}
        </li>
      )}
      <li className="flex items-center gap-0.5">
        <span className={cn("size-2.5 rounded-sm border border-dashed", holidayStyle.border)} />
        {t("optional")}
      </li>
      <li className="flex items-center gap-0.5">
        <span className="size-2.5 rounded-sm ring-2 ring-brand ring-inset" />
        {t("today")}
      </li>
    </ul>
  )
}

function HolidayRow({
  holiday,
  canWrite,
  onEdit,
  onDelete,
}: {
  holiday: Holiday
  canWrite: boolean
  onEdit: () => void
  onDelete: () => void
}) {
  const t = useTranslations("scheduling.holidays")
  const date = parseISO(holiday.holidayDate)
  return (
    <li className="group flex items-start gap-1 border-b border-rule px-1.5 py-1">
      <span
        className={cn(
          "flex w-5 shrink-0 flex-col items-center rounded-sm border py-0.25 leading-tight",
          holiday.isOptional ? cn("border-dashed", holidayStyle.border, holidayStyle.text) : cn(holidayStyle.tint, holidayStyle.border, holidayStyle.text),
        )}
      >
        <span className="font-mono text-14 font-semibold">{format(date, "dd")}</span>
        <span className="text-12">{format(date, "MMM")}</span>
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-14 font-medium">{holiday.name}</p>
        <p className="flex flex-wrap items-center gap-0.5 text-12 text-ink-muted">
          <span>{format(date, "EEEE")}</span>
          {holiday.isOptional && <Tag>{t("optional")}</Tag>}
          {holiday.workLocations.length > 0 ? (
            holiday.workLocations.map((location) => (
              <Tag key={location.publicId} title={location.name}>
                <span className="font-mono">{location.code}</span>
              </Tag>
            ))
          ) : (
            <span>· {t("everywhere")}</span>
          )}
        </p>
      </div>
      <div className="flex shrink-0 gap-0.25">
        <Button variant="ghost" size="icon-sm" onClick={onEdit} aria-label={t(canWrite ? "edit" : "view", { name: holiday.name })}>
          <PencilIcon aria-hidden="true" />
        </Button>
        {canWrite && (
          <Button variant="ghost" size="icon-sm" onClick={onDelete} aria-label={t("remove", { name: holiday.name })}>
            <Trash2Icon aria-hidden="true" />
          </Button>
        )}
      </div>
    </li>
  )
}

function Tag({ children, title }: { children: React.ReactNode; title?: string }) {
  return (
    <span title={title} className="inline-flex h-2.25 items-center rounded-pill border border-rule bg-surface px-0.5 text-12 text-ink">
      {children}
    </span>
  )
}

function HolidayDialog({
  holiday,
  date,
  readOnly,
  onClose,
  onDelete,
}: {
  holiday?: Holiday
  date?: string
  readOnly: boolean
  onClose: () => void
  onDelete?: () => void
}) {
  const t = useTranslations("scheduling.holidays")
  const tCommon = useTranslations("common")
  const errorText = useErrorText()
  const messageText = useMessageText()
  const locations = useWorkLocations()
  const { save } = useHolidayMutations()
  const [formError, setFormError] = useState<string | null>(null)
  const [everywhere, setEverywhere] = useState(!holiday || holiday.workLocations.length === 0)
  const [locationsError, setLocationsError] = useState<string | null>(null)

  const form = useForm<HolidayInput, unknown, HolidayOutput>({
    resolver: zodResolver(holidaySchema),
    defaultValues: {
      name: holiday?.name ?? "",
      holidayDate: holiday?.holidayDate ?? date ?? "",
      isOptional: holiday?.isOptional ?? false,
      description: holiday?.description ?? "",
      workLocationIds: holiday?.workLocations.map((l) => l.publicId) ?? [],
    },
  })
  const selectedIds = useWatch({ control: form.control, name: "workLocationIds" }) ?? []

  // Active locations to choose from, plus any retired one the holiday is already limited to: the API keeps
  // an unchanged link to a retired location.
  const options = (locations.data ?? [])
    .filter((l) => l.isActive || holiday?.workLocations.some((linked) => linked.publicId === l.publicId))
    .map((l) => ({ id: l.publicId, code: l.code, name: l.isActive ? l.name : t("retiredLocation", { name: l.name }) }))

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)
    setLocationsError(null)
    if (!everywhere && values.workLocationIds.length === 0) {
      setLocationsError(t("chooseLocation"))
      return
    }
    try {
      const saved = await save.mutateAsync({
        id: holiday?.publicId,
        values: { ...values, workLocationIds: everywhere ? [] : values.workLocationIds },
      })
      toast.success(holiday ? t("saved", { name: saved.name }) : t("created", { name: saved.name }))
      onClose()
    } catch (error) {
      const applied = applyApiErrors(error, form.setError, ["name", "holidayDate", "isOptional", "description"], {
        "holiday.duplicate": "name",
      })
      if (!applied) setFormError(errorText(error))
    }
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-[560px]">
        <form onSubmit={onSubmit} noValidate className="flex flex-col gap-2">
          <DialogHeader>
            <DialogTitle>{holiday ? (readOnly ? holiday.name : t("editTitle", { name: holiday.name })) : t("addTitle")}</DialogTitle>
            <DialogDescription>{t("dialogDescription")}</DialogDescription>
          </DialogHeader>
          <FormAlert>{formError}</FormAlert>

          <div className="grid grid-cols-[1fr_180px] gap-2">
            <TextField control={form.control} name="name" label={t("fields.name")} required disabled={readOnly} autoFocus={!readOnly} />
            <TextField control={form.control} name="holidayDate" label={t("fields.date")} type="date" mono required disabled={readOnly} />
          </div>

          <Controller
            control={form.control}
            name="isOptional"
            render={({ field }) => (
              <label className="flex items-start gap-1 text-14">
                <Checkbox
                  checked={!!field.value}
                  onCheckedChange={(checked) => field.onChange(checked === true)}
                  disabled={readOnly}
                  aria-label={t("fields.optional")}
                  aria-describedby="holiday-optional-hint"
                  className="mt-0.25"
                />
                <span>
                  <span className="font-medium">{t("fields.optional")}</span>
                  <span id="holiday-optional-hint" className="block text-13 text-ink-muted">
                    {t("hints.optional")}
                  </span>
                </span>
              </label>
            )}
          />

          <fieldset className="flex flex-col gap-1" aria-describedby={locationsError ? "holiday-locations-error" : undefined}>
            <legend className="mb-0.5 text-14 font-medium">{t("fields.locations")}</legend>
            <div role="radiogroup" aria-label={t("fields.locations")} className="inline-flex w-fit rounded-md border border-rule p-0.25">
              {[true, false].map((value) => (
                <button
                  key={String(value)}
                  type="button"
                  role="radio"
                  aria-checked={everywhere === value}
                  disabled={readOnly}
                  onClick={() => {
                    setEverywhere(value)
                    setLocationsError(null)
                  }}
                  className={cn(
                    "h-3.5 rounded-sm px-1.25 text-13 font-medium",
                    everywhere === value ? "bg-brand text-primary-foreground" : "text-ink-muted hover:text-ink",
                  )}
                >
                  {value ? t("everywhere") : t("someLocations")}
                </button>
              ))}
            </div>
            {!everywhere && (
              <div className="grid grid-cols-2 gap-x-2 gap-y-0.5 rounded-md border border-rule p-1">
                {locations.isPending ? (
                  <Spinner />
                ) : (
                  options.map((option) => (
                    <label key={option.id} className="flex items-center gap-1 text-14">
                      <Checkbox
                        checked={selectedIds.includes(option.id)}
                        disabled={readOnly}
                        aria-label={`${option.code} ${option.name}`}
                        onCheckedChange={(checked) => {
                          setLocationsError(null)
                          form.setValue(
                            "workLocationIds",
                            checked ? [...selectedIds, option.id] : selectedIds.filter((id) => id !== option.id),
                            { shouldDirty: true },
                          )
                        }}
                      />
                      <span className="font-mono text-13 text-ink-muted">{option.code}</span>
                      <span className="truncate">{option.name}</span>
                    </label>
                  ))
                )}
              </div>
            )}
            <p className="text-13 text-ink-muted">{everywhere ? t("hints.everywhere") : t("hints.someLocations")}</p>
            {locationsError && (
              <p id="holiday-locations-error" role="alert" className="text-13 text-danger">
                {messageText(locationsError)}
              </p>
            )}
          </fieldset>

          <CustomField
            control={form.control}
            name="description"
            label={t("fields.description")}
            disabled={readOnly}
            render={(field, control) => (
              <Textarea
                {...control}
                value={(field.value as string | null) ?? ""}
                onChange={(event) => field.onChange(event.target.value)}
                onBlur={field.onBlur}
                disabled={readOnly}
                maxLength={500}
                rows={2}
                className="min-h-6"
              />
            )}
          />

          {holiday && !readOnly && <p className="text-13 text-ink-muted">{t("noRecalculation")}</p>}

          <DialogFooter className="sm:justify-between">
            <div>
              {onDelete && (
                <Button type="button" variant="destructive" onClick={onDelete}>
                  <Trash2Icon aria-hidden="true" />
                  {t("removeShort")}
                </Button>
              )}
            </div>
            <div className="flex gap-1">
              <Button type="button" variant="outline" onClick={onClose}>
                {readOnly ? t("close") : tCommon("cancel")}
              </Button>
              {!readOnly && (
                <Button type="submit" disabled={form.formState.isSubmitting}>
                  {form.formState.isSubmitting && <Spinner />}
                  {holiday ? tCommon("save") : t("add")}
                </Button>
              )}
            </div>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/**
 * Removing a holiday is a soft delete: it leaves the calendar but stays in the history. The API takes no
 * reason, so the dialog states the consequence instead (the same gap as retiring master data, §4.5).
 */
function DeleteHolidayDialog({ holiday, onClose }: { holiday: Holiday; onClose: () => void }) {
  const t = useTranslations("scheduling.holidays")
  const tCommon = useTranslations("common")
  const errorText = useErrorText()
  const { remove } = useHolidayMutations()
  const [error, setError] = useState<string | null>(null)

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-[440px]">
        <DialogHeader>
          <DialogTitle>{t("delete.title", { name: holiday.name })}</DialogTitle>
          <DialogDescription>
            {t("delete.description", { date: format(parseISO(holiday.holidayDate), "EEEE d MMMM yyyy") })}
          </DialogDescription>
        </DialogHeader>
        <FormAlert>{error}</FormAlert>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>
            {tCommon("cancel")}
          </Button>
          <Button
            type="button"
            variant="destructive"
            disabled={remove.isPending}
            onClick={async () => {
              setError(null)
              try {
                await remove.mutateAsync(holiday.publicId)
                toast.success(t("delete.done", { name: holiday.name }))
                onClose()
              } catch (failure) {
                setError(errorText(failure))
              }
            }}
          >
            {remove.isPending && <Spinner />}
            {t("delete.confirm")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

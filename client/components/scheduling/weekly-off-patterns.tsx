"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { ArchiveIcon, ArchiveRestoreIcon, InfoIcon, PlusIcon, SearchIcon } from "lucide-react"
import { useTranslations } from "next-intl"
import { useId, useMemo, useState } from "react"
import { Controller, useForm, useWatch } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { ActiveBadge } from "@/components/employees/active-badge"
import { applyApiErrors } from "@/components/forms/api-errors"
import { TextField } from "@/components/forms/fields"
import { FormAlert, FormNotice } from "@/components/forms/form-alert"
import { SetActiveDialog } from "@/components/org/org-structure"
import { PageHeader } from "@/components/page-header"
import { useSession } from "@/components/shell/session-context"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Skeleton } from "@/components/ui/skeleton"
import { Spinner } from "@/components/ui/spinner"
import { useErrorText, useMessageText } from "@/lib/api/errors"
import { useWeeklyOffPatternMutations, useWeeklyOffPatterns } from "@/lib/api/scheduling-queries"
import type { WeeklyOffPattern } from "@/lib/api/scheduling-types"
import { todayInBusinessZone } from "@/lib/dates"
import { Permissions } from "@/lib/permissions"
import {
  renameWeeklyOffPatternSchema,
  weeklyOffPatternSchema,
  type WeeklyOffPatternInput,
  type WeeklyOffPatternOutput,
} from "@/lib/schemas/scheduling"
import { useUrlState } from "@/lib/use-url-state"
import { cn } from "@/lib/utils"

import { PATTERN_PRESETS, PatternPreview, PatternStrip, WeeklyOffGrid, usePatternSummary } from "./weekly-off-grid"

/** The list's filter and the open pattern, in the URL (§4.5). `selected=new` opens the editor. */
const paramsSchema = z.object({
  q: z.string().trim().max(100).catch("").default(""),
  status: z.enum(["active", "retired", "all"]).catch("active").default("active"),
  selected: z.union([z.guid(), z.literal("new")]).optional().catch(undefined),
})

/**
 * Screen 30 (FR-2.2): patterns on the left, the open one on the right. A pattern's days are fixed once it
 * exists, so past weekly offs never change; only its name can be edited afterwards.
 */
export function WeeklyOffPatterns() {
  const t = useTranslations("scheduling.patterns")
  const errorText = useErrorText()
  const summary = usePatternSummary()
  const { can } = useSession()
  const canWrite = can(Permissions.Shift.Write)
  const patterns = useWeeklyOffPatterns()
  const [params, setParams] = useUrlState(paramsSchema)

  const filtered = useMemo(() => {
    const term = params.q.toLowerCase()
    return (patterns.data ?? []).filter(
      (pattern) =>
        (params.status === "all" || pattern.isActive === (params.status === "active")) &&
        (!term || pattern.name.toLowerCase().includes(term)),
    )
  }, [patterns.data, params.q, params.status])

  const selected = patterns.data?.find((pattern) => pattern.publicId === params.selected)

  return (
    <div className="flex h-full flex-col">
      <PageHeader
        title={t("title")}
        description={t("description")}
        actions={
          canWrite && (
            <Button onClick={() => setParams({ selected: "new" })}>
              <PlusIcon aria-hidden="true" />
              {t("add")}
            </Button>
          )
        }
      />

      <div className="flex min-h-0 flex-1">
        <aside aria-label={t("listLabel")} className="flex w-[320px] shrink-0 flex-col border-r border-rule bg-surface">
          <div role="search" className="flex gap-1 border-b border-rule p-1">
            <div className="relative min-w-0 flex-1">
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
              <SelectTrigger aria-label={t("status")} className="w-[112px]">
                <SelectValue>{t(`statuses.${params.status}`)}</SelectValue>
              </SelectTrigger>
              <SelectContent position="popper">
                {(["active", "retired", "all"] as const).map((status) => (
                  <SelectItem key={status} value={status}>
                    {t(`statuses.${status}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="relative min-h-0 flex-1 overflow-auto" aria-busy={patterns.isPending || undefined}>
            {patterns.isPending ? (
              <ul aria-hidden="true">
                {Array.from({ length: 4 }, (_, index) => (
                  <li key={index} className="border-b border-rule px-1.5 py-1">
                    <Skeleton className="h-2 w-18" />
                    <Skeleton className="mt-0.5 h-2.5 w-20" />
                  </li>
                ))}
              </ul>
            ) : patterns.isError ? (
              <div className="p-1.5">
                <FormAlert>{errorText(patterns.error)}</FormAlert>
              </div>
            ) : filtered.length === 0 ? (
              <p className="p-1.5 text-14 text-ink-muted">{params.q ? t("emptyFiltered") : t(`empty.${params.status}`)}</p>
            ) : (
              <ul>
                {filtered.map((pattern) => {
                  const active = pattern.publicId === params.selected
                  return (
                    <li key={pattern.publicId}>
                      <button
                        type="button"
                        aria-current={active || undefined}
                        onClick={() => setParams({ selected: pattern.publicId })}
                        className={cn(
                          "flex w-full flex-col gap-0.5 border-b border-rule px-1.5 py-1 text-left hover:bg-canvas",
                          active && "bg-brand-soft hover:bg-brand-soft",
                        )}
                      >
                        <span className="flex items-center justify-between gap-1">
                          <span className="truncate text-14 font-medium">{pattern.name}</span>
                          {!pattern.isActive && <ActiveBadge active={false} />}
                        </span>
                        <PatternStrip days={pattern.days} />
                        <span className="text-12 text-ink-muted">{summary(pattern.days)}</span>
                      </button>
                    </li>
                  )
                })}
              </ul>
            )}
          </div>
        </aside>

        <main className="relative min-w-0 flex-1 overflow-auto px-3 py-3">
          {params.selected === "new" && canWrite ? (
            <PatternCreator
              onCreated={(pattern) => setParams({ selected: pattern.publicId, status: "active", q: "" })}
              onCancel={() => setParams({ selected: undefined })}
            />
          ) : selected ? (
            <PatternView key={selected.publicId} pattern={selected} canWrite={canWrite} />
          ) : (
            <div className="flex max-w-form flex-col items-start gap-1 rounded-md border border-dashed border-rule bg-surface p-3 text-14 text-ink-muted">
              <h2 className="text-16 font-semibold text-ink">{t("pick.title")}</h2>
              <p>{canWrite ? t("pick.bodyWrite") : t("pick.body")}</p>
            </div>
          )}
        </main>
      </div>
    </div>
  )
}

function PatternCreator({
  onCreated,
  onCancel,
}: {
  onCreated: (pattern: WeeklyOffPattern) => void
  onCancel: () => void
}) {
  const t = useTranslations("scheduling.patterns")
  const tCommon = useTranslations("common")
  const errorText = useErrorText()
  const messageText = useMessageText()
  const summary = usePatternSummary()
  const { create } = useWeeklyOffPatternMutations()
  const [formError, setFormError] = useState<string | null>(null)
  const daysErrorId = useId()

  const form = useForm<WeeklyOffPatternInput, unknown, WeeklyOffPatternOutput>({
    resolver: zodResolver(weeklyOffPatternSchema),
    defaultValues: { name: "", days: PATTERN_PRESETS[1].days },
  })
  const days = useWatch({ control: form.control, name: "days" }) ?? []

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)
    try {
      const pattern = await create.mutateAsync(values)
      toast.success(t("created", { name: pattern.name }))
      onCreated(pattern)
    } catch (error) {
      if (!applyApiErrors(error, form.setError, ["name", "days"], { "weekly_off_pattern.name_taken": "name" })) {
        setFormError(errorText(error))
      }
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate className="flex max-w-form flex-col gap-2">
      <h2 className="text-20 font-semibold">{t("addTitle")}</h2>
      <FormAlert>{formError}</FormAlert>
      <TextField control={form.control} name="name" label={t("fields.name")} description={t("hints.name")} required autoFocus />

      <section className="rounded-md border border-rule bg-surface p-3">
        <div className="mb-1.5 flex flex-wrap items-center justify-between gap-1">
          <h3 className="text-16 font-semibold">{t("fields.days")}</h3>
          <div className="flex flex-wrap gap-0.5" role="group" aria-label={t("presets.label")}>
            {PATTERN_PRESETS.map((preset) => (
              <Button
                key={preset.key}
                type="button"
                size="xs"
                variant="outline"
                onClick={() => form.setValue("days", preset.days, { shouldValidate: form.formState.isSubmitted })}
              >
                {t(`presets.${preset.key}`)}
              </Button>
            ))}
          </div>
        </div>
        <Controller
          control={form.control}
          name="days"
          render={({ field, fieldState }) => (
            <>
              <WeeklyOffGrid
                value={field.value}
                onChange={(next) => {
                  field.onChange(next)
                  if (form.formState.isSubmitted) void form.trigger("days")
                }}
                invalid={!!fieldState.error}
                describedBy={fieldState.error ? daysErrorId : undefined}
              />
              {fieldState.error && (
                <p id={daysErrorId} role="alert" className="mt-1 text-13 text-danger">
                  {messageText(fieldState.error.message)}
                </p>
              )}
            </>
          )}
        />
        <p className="mt-1.5 text-14" aria-live="polite">
          <span className="font-medium">{t("summaryLabel")}</span> {summary(days)}
        </p>
        <WeekOfMonthNote />
      </section>

      <section aria-labelledby="pattern-preview">
        <h3 id="pattern-preview" className="mb-1 text-14 font-semibold">
          {t("preview.title")}
        </h3>
        <PatternPreview days={days} from={todayInBusinessZone()} />
      </section>

      <FormNotice>{t("fixedNotice")}</FormNotice>

      <div className="flex justify-end gap-1">
        <Button type="button" variant="outline" onClick={onCancel}>
          {tCommon("cancel")}
        </Button>
        <Button type="submit" disabled={form.formState.isSubmitting}>
          {form.formState.isSubmitting && <Spinner />}
          {t("add")}
        </Button>
      </div>
    </form>
  )
}

function PatternView({ pattern, canWrite }: { pattern: WeeklyOffPattern; canWrite: boolean }) {
  const t = useTranslations("scheduling.patterns")
  const summary = usePatternSummary()
  const { setActive } = useWeeklyOffPatternMutations()
  const [confirming, setConfirming] = useState(false)

  return (
    <div className="flex max-w-form flex-col gap-2">
      <div className="flex flex-wrap items-start justify-between gap-1">
        <div className="min-w-0">
          <h2 className="flex flex-wrap items-center gap-1 text-20 font-semibold">
            {pattern.name}
            <ActiveBadge active={pattern.isActive} />
          </h2>
          <p className="mt-0.25 text-14 text-ink-muted">{summary(pattern.days)}</p>
        </div>
        {canWrite && (
          <Button variant="outline" onClick={() => setConfirming(true)}>
            {pattern.isActive ? <ArchiveIcon aria-hidden="true" /> : <ArchiveRestoreIcon aria-hidden="true" />}
            {pattern.isActive ? t("retire.action") : t("restore.action")}
          </Button>
        )}
      </div>

      {canWrite && <RenameForm pattern={pattern} />}

      <section className="rounded-md border border-rule bg-surface p-3">
        <h3 className="mb-1.5 text-16 font-semibold">{t("fields.days")}</h3>
        <WeeklyOffGrid value={pattern.days} readOnly />
        <WeekOfMonthNote />
      </section>

      <section aria-labelledby="pattern-preview">
        <h3 id="pattern-preview" className="mb-1 text-14 font-semibold">
          {t("preview.title")}
        </h3>
        <PatternPreview days={pattern.days} from={todayInBusinessZone()} />
      </section>

      <FormNotice>{t("fixedNotice")}</FormNotice>

      <SetActiveDialog
        open={confirming}
        onOpenChange={setConfirming}
        retiring={pattern.isActive}
        title={t(pattern.isActive ? "retire.title" : "restore.title", { name: pattern.name })}
        description={t(pattern.isActive ? "retire.description" : "restore.description")}
        confirmLabel={t(pattern.isActive ? "retire.confirm" : "restore.confirm")}
        onConfirm={async () => {
          await setActive.mutateAsync({ id: pattern.publicId, active: !pattern.isActive })
          toast.success(t(pattern.isActive ? "retire.done" : "restore.done", { name: pattern.name }))
        }}
      />
    </div>
  )
}

function RenameForm({ pattern }: { pattern: WeeklyOffPattern }) {
  const t = useTranslations("scheduling.patterns")
  const errorText = useErrorText()
  const { rename } = useWeeklyOffPatternMutations()
  const [formError, setFormError] = useState<string | null>(null)
  const form = useForm<{ name: string }>({
    resolver: zodResolver(renameWeeklyOffPatternSchema),
    defaultValues: { name: pattern.name },
  })

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null)
    try {
      const renamed = await rename.mutateAsync({ id: pattern.publicId, name: values.name })
      form.reset({ name: renamed.name })
      toast.success(t("renamed", { name: renamed.name }))
    } catch (error) {
      if (!applyApiErrors(error, form.setError, ["name"], { "weekly_off_pattern.name_taken": "name" })) {
        setFormError(errorText(error))
      }
    }
  })

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-1 rounded-md border border-rule bg-surface p-3">
      <FormAlert>{formError}</FormAlert>
      <div className="flex items-end gap-1">
        <TextField control={form.control} name="name" label={t("fields.name")} required className="flex-1" />
        <Button type="submit" variant="outline" disabled={!form.formState.isDirty || form.formState.isSubmitting}>
          {form.formState.isSubmitting && <Spinner />}
          {t("rename")}
        </Button>
      </div>
    </form>
  )
}

/** The one rule people get wrong: "week 2" is the second such weekday of the month, not a calendar row. */
function WeekOfMonthNote() {
  const t = useTranslations("scheduling.patterns")
  return (
    <p className="mt-1.5 flex items-start gap-0.5 text-13 text-ink-muted">
      <InfoIcon aria-hidden="true" className="mt-0.25 size-1.75 shrink-0" />
      {t("weekOfMonthNote")}
    </p>
  )
}

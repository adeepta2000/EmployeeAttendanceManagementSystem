"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { ArrowLeftIcon, MoonIcon } from "lucide-react"
import Link from "next/link"
import { useRouter } from "next/navigation"
import { useTranslations } from "next-intl"
import { useState, type ReactNode } from "react"
import { useForm, useWatch, type Control, type FieldPath } from "react-hook-form"
import { toast } from "sonner"

import { ActiveBadge } from "@/components/employees/active-badge"
import { applyApiErrors } from "@/components/forms/api-errors"
import { TextField } from "@/components/forms/fields"
import { FormAlert } from "@/components/forms/form-alert"
import { PageHeader } from "@/components/page-header"
import { useSession } from "@/components/shell/session-context"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { Spinner } from "@/components/ui/spinner"
import { ApiError } from "@/lib/api/client"
import { useErrorText } from "@/lib/api/errors"
import { useShift, useShiftMutations } from "@/lib/api/scheduling-queries"
import type { Shift } from "@/lib/api/scheduling-types"
import { Permissions } from "@/lib/permissions"
import { MAX_GRACE_MINUTES, shiftSchema, type ShiftInput, type ShiftOutput } from "@/lib/schemas/scheduling"
import { formatHours, formatTime, parseTime, shiftDuration } from "@/lib/shift-time"

import { ShiftTimeline, Time, WorkedTimeScale, type ShiftShape } from "./shift-timeline"

const FIELDS: FieldPath<ShiftInput>[] = [
  "code",
  "name",
  "startTime",
  "endTime",
  "breakMinutes",
  "graceInMinutes",
  "graceOutMinutes",
  "halfDayThresholdMinutes",
  "fullDayMinutes",
  "punchWindowBeforeMinutes",
  "punchWindowAfterMinutes",
]

/** A new shift starts as a typical day shift, so the timeline has something to show at once. */
const NEW_SHIFT: ShiftInput = {
  code: "",
  name: "",
  startTime: "09:00",
  endTime: "18:00",
  breakMinutes: "60",
  graceInMinutes: "10",
  graceOutMinutes: "5",
  halfDayThresholdMinutes: "240",
  fullDayMinutes: "480",
  punchWindowBeforeMinutes: "120",
  punchWindowAfterMinutes: "240",
}

function toFormValues(shift: Shift): ShiftInput {
  return {
    code: shift.code,
    name: shift.name,
    startTime: shift.startTime.slice(0, 5),
    endTime: shift.endTime.slice(0, 5),
    breakMinutes: String(shift.breakMinutes),
    graceInMinutes: String(shift.graceInMinutes),
    graceOutMinutes: String(shift.graceOutMinutes),
    halfDayThresholdMinutes: String(shift.halfDayThresholdMinutes),
    fullDayMinutes: String(shift.fullDayMinutes),
    punchWindowBeforeMinutes: String(shift.punchWindowBeforeMinutes),
    punchWindowAfterMinutes: String(shift.punchWindowAfterMinutes),
  }
}

/** Screen 29, one shift: loads it, then edits it in place. */
export function ShiftDetail({ id }: { id: string }) {
  const t = useTranslations("scheduling.shifts")
  const errorText = useErrorText()
  const query = useShift(id)

  if (query.isPending) return <EditorSkeleton />
  if (query.isError) {
    const notFound = query.error instanceof ApiError && query.error.status === 404
    return (
      <div className="flex flex-col items-start gap-2 p-3">
        <FormAlert>{notFound ? t("notFound") : errorText(query.error)}</FormAlert>
        <BackToShifts />
      </div>
    )
  }
  return <ShiftEditor shift={query.data} />
}

/**
 * Screen 29's editor (FR-2.1). The timeline at the top redraws as each field changes, and every section
 * says in words what its numbers will do on a real day: grace and punch windows are the settings people
 * most often get wrong, so the screen explains them with the values being typed rather than a help page.
 */
export function ShiftEditor({ shift }: { shift?: Shift }) {
  const t = useTranslations("scheduling.shifts")
  const tCommon = useTranslations("common")
  const errorText = useErrorText()
  const router = useRouter()
  const { can } = useSession()
  const canWrite = can(Permissions.Shift.Write)
  const { save } = useShiftMutations()
  const [formError, setFormError] = useState<string | null>(null)

  const form = useForm<ShiftInput, unknown, ShiftOutput>({
    resolver: zodResolver(shiftSchema),
    defaultValues: shift ? toFormValues(shift) : NEW_SHIFT,
  })
  const values = useWatch({ control: form.control })
  const shape = toShape(values)

  const onSubmit = form.handleSubmit(async (output) => {
    setFormError(null)
    try {
      const saved = await save.mutateAsync({ id: shift?.publicId, values: output })
      toast.success(shift ? t("saved", { name: saved.name }) : t("created", { name: saved.name }))
      router.push("/setup/shifts")
    } catch (error) {
      if (!applyApiErrors(error, form.setError, FIELDS, { "shift.code_taken": "code" })) setFormError(errorText(error))
    }
  })

  const disabled = !canWrite || form.formState.isSubmitting
  const control = form.control

  return (
    <form onSubmit={onSubmit} noValidate className="flex h-full flex-col">
      <PageHeader
        title={
          shift ? (
            <span className="flex flex-wrap items-center gap-1">
              {t("editTitle", { name: shift.name })}
              <ActiveBadge active={shift.isActive} />
            </span>
          ) : (
            t("addTitle")
          )
        }
        description={shift ? t("editDescription") : t("addDescription")}
        actions={<BackToShifts />}
      />

      {/* relative: the scroll area is the containing block of the sr-only text inside, so it can't
          stretch the page below the fold. */}
      <div className="relative min-h-0 flex-1 overflow-auto">
        {/* The preview stays in view while the fields below scroll. */}
        <section aria-labelledby="shift-preview" className="sticky top-0 z-10 border-b border-rule bg-surface px-3 pt-2 pb-1.5">
          <div className="max-w-[960px]">
            <div className="mb-1 flex flex-wrap items-baseline justify-between gap-1">
              <h2 id="shift-preview" className="text-14 font-semibold">
                {t("preview")}
              </h2>
              <Summary shape={shape} />
            </div>
            <ShiftTimeline shape={shape} />
            <TimelineKey />
          </div>
        </section>

        <div className="flex max-w-form flex-col gap-3 px-3 py-3">
          <FormAlert>{!canWrite ? t("readOnly") : null}</FormAlert>

          <Section title={t("sections.identity")}>
            <div className="grid grid-cols-[200px_1fr] gap-2">
              <TextField control={control} name="code" label={t("fields.code")} description={t("hints.code")} mono required disabled={disabled} autoFocus={!shift} />
              <TextField control={control} name="name" label={t("fields.name")} required disabled={disabled} />
            </div>
          </Section>

          <Section title={t("sections.hours")} explain={<HoursExplainer shape={shape} />}>
            <div className="grid grid-cols-3 gap-2">
              <TextField control={control} name="startTime" label={t("fields.startTime")} type="time" mono required disabled={disabled} />
              <TextField control={control} name="endTime" label={t("fields.endTime")} type="time" mono required disabled={disabled} />
              <MinutesField control={control} name="breakMinutes" label={t("fields.breakMinutes")} disabled={disabled} />
            </div>
          </Section>

          <Section title={t("sections.grace")} explain={<GraceExplainer shape={shape} />}>
            <div className="grid grid-cols-2 gap-2">
              <MinutesField
                control={control}
                name="graceInMinutes"
                label={t("fields.graceInMinutes")}
                description={t("hints.graceIn", { max: MAX_GRACE_MINUTES })}
                disabled={disabled}
              />
              <MinutesField
                control={control}
                name="graceOutMinutes"
                label={t("fields.graceOutMinutes")}
                description={t("hints.graceOut", { max: MAX_GRACE_MINUTES })}
                disabled={disabled}
              />
            </div>
          </Section>

          <Section title={t("sections.thresholds")} explain={<ThresholdExplainer shape={shape} />}>
            <div className="grid grid-cols-2 gap-2">
              <MinutesField
                control={control}
                name="fullDayMinutes"
                label={t("fields.fullDayMinutes")}
                description={t("hints.fullDay")}
                disabled={disabled}
              />
              <MinutesField
                control={control}
                name="halfDayThresholdMinutes"
                label={t("fields.halfDayThresholdMinutes")}
                description={t("hints.halfDay")}
                disabled={disabled}
              />
            </div>
            <div className="mt-2">
              <WorkedTimeScale shape={shape} />
            </div>
          </Section>

          <Section title={t("sections.window")} explain={<WindowExplainer shape={shape} />}>
            <div className="grid grid-cols-2 gap-2">
              <MinutesField
                control={control}
                name="punchWindowBeforeMinutes"
                label={t("fields.punchWindowBeforeMinutes")}
                description={t("hints.windowBefore")}
                disabled={disabled}
              />
              <MinutesField
                control={control}
                name="punchWindowAfterMinutes"
                label={t("fields.punchWindowAfterMinutes")}
                description={t("hints.windowAfter")}
                disabled={disabled}
              />
            </div>
          </Section>
        </div>
      </div>

      <div className="flex shrink-0 items-center gap-2 border-t border-rule bg-surface px-3 py-1.5">
        <div className="min-w-0 flex-1">
          <FormAlert>{formError}</FormAlert>
        </div>
        <Button type="button" variant="outline" onClick={() => router.push("/setup/shifts")} disabled={form.formState.isSubmitting}>
          {tCommon("cancel")}
        </Button>
        {canWrite && (
          <Button type="submit" disabled={form.formState.isSubmitting}>
            {form.formState.isSubmitting && <Spinner />}
            {shift ? tCommon("save") : t("add")}
          </Button>
        )}
      </div>
    </form>
  )
}

/** The form's text values as numbers for the drawing; anything not yet a number counts as zero. */
function toShape(values: Partial<ShiftInput>): ShiftShape {
  const minutes = (value: string | number | undefined) => {
    const number = Number(value)
    return value === "" || !Number.isFinite(number) || number < 0 ? 0 : Math.floor(number)
  }
  return {
    start: parseTime(values.startTime),
    end: parseTime(values.endTime),
    breakMinutes: minutes(values.breakMinutes),
    graceInMinutes: minutes(values.graceInMinutes),
    graceOutMinutes: minutes(values.graceOutMinutes),
    halfDayThresholdMinutes: minutes(values.halfDayThresholdMinutes),
    fullDayMinutes: minutes(values.fullDayMinutes),
    punchWindowBeforeMinutes: minutes(values.punchWindowBeforeMinutes),
    punchWindowAfterMinutes: minutes(values.punchWindowAfterMinutes),
  }
}

/** Minutes, as the API stores them, with the hours alongside once there are enough of them. */
function MinutesField({
  control,
  name,
  label,
  description,
  disabled,
}: {
  control: Control<ShiftInput>
  name: FieldPath<ShiftInput>
  label: string
  description?: ReactNode
  disabled?: boolean
}) {
  const t = useTranslations("scheduling.shifts")
  const value = Number(useWatch({ control, name }))
  return (
    <TextField
      control={control}
      name={name}
      label={label}
      description={description}
      inputMode="numeric"
      mono
      required
      disabled={disabled}
      suffix={
        <>
          {t("minutesUnit")}
          {Number.isInteger(value) && value >= 60 && <span className="font-mono text-ink-muted">= {formatHours(value)} h</span>}
        </>
      }
    />
  )
}

function Section({ title, explain, children }: { title: string; explain?: ReactNode; children: ReactNode }) {
  return (
    <section className="rounded-md border border-rule bg-surface p-3">
      <h2 className="mb-2 text-16 font-semibold">{title}</h2>
      {children}
      {explain && <div className="mt-2 rounded-md bg-canvas px-1.5 py-1 text-13 text-ink [&_p+p]:mt-0.5">{explain}</div>}
    </section>
  )
}

function Summary({ shape }: { shape: ShiftShape }) {
  const t = useTranslations("scheduling.shifts")
  if (shape.start === null || shape.end === null) return null
  const duration = shiftDuration(shape.start, shape.end)
  if (duration === 0) return null
  const night = shape.start + duration > 24 * 60
  return (
    <p className="flex items-center gap-1 text-13 text-ink-muted">
      {night && <MoonIcon aria-hidden="true" className="size-1.75" />}
      {t(night ? "summaryNight" : "summaryDay", {
        start: formatTime(shape.start),
        end: formatTime(shape.end),
        length: formatHours(duration),
        paid: formatHours(Math.max(0, duration - shape.breakMinutes)),
      })}
    </p>
  )
}

/** The key under the timeline: what each kind of mark means. */
function TimelineKey() {
  const t = useTranslations("scheduling.timeline.key")
  return (
    <ul className="mt-1 flex flex-wrap gap-x-2 gap-y-0.5 text-12 text-ink-muted" aria-label={t("label")}>
      <li className="flex items-center gap-0.5">
        <span className="h-1.5 w-2.5 rounded-sm border border-dashed border-ink-muted/50 bg-canvas" />
        {t("window")}
      </li>
      <li className="flex items-center gap-0.5">
        <span className="h-1.5 w-2.5 rounded-sm border border-brand bg-brand-soft" />
        {t("shift")}
      </li>
      <li className="flex items-center gap-0.5">
        <span
          className="h-1.5 w-2.5 rounded-sm border border-brand/60 bg-brand-soft"
          style={{ backgroundImage: "repeating-linear-gradient(135deg, var(--brand) 0 1.5px, transparent 1.5px 5px)" }}
        />
        {t("grace")}
      </li>
      <li className="flex items-center gap-0.5">
        <span
          className="h-1.5 w-2.5 rounded-sm border border-ink-muted/40 bg-surface"
          style={{ backgroundImage: "repeating-linear-gradient(45deg, var(--ink-muted) 0 1px, transparent 1px 4px)" }}
        />
        {t("break")}
      </li>
    </ul>
  )
}

function HoursExplainer({ shape }: { shape: ShiftShape }) {
  const t = useTranslations("scheduling.shifts.explain")
  if (shape.start === null || shape.end === null) return <p>{t("enterTimes")}</p>
  const duration = shiftDuration(shape.start, shape.end)
  if (duration === 0) return <p>{t("sameTimes")}</p>
  const night = shape.start + duration > 24 * 60
  return (
    <>
      <p>
        {t.rich(night ? "night" : "day", {
          start: () => <Time minutes={shape.start!} />,
          end: () => <Time minutes={shape.start! + duration} />,
          length: formatHours(duration),
        })}
      </p>
      <p>
        {shape.breakMinutes > 0
          ? t("break", { minutes: shape.breakMinutes, paid: formatHours(Math.max(0, duration - shape.breakMinutes)) })
          : t("noBreak")}
      </p>
    </>
  )
}

function GraceExplainer({ shape }: { shape: ShiftShape }) {
  const t = useTranslations("scheduling.shifts.explain")
  if (shape.start === null || shape.end === null || shiftDuration(shape.start, shape.end) === 0) return null
  const end = shape.start + shiftDuration(shape.start, shape.end)
  return (
    <>
      <p>
        {shape.graceInMinutes > 0
          ? t.rich("graceIn", {
              start: () => <Time minutes={shape.start!} />,
              until: () => <Time minutes={shape.start! + shape.graceInMinutes} />,
              minutes: shape.graceInMinutes,
            })
          : t.rich("noGraceIn", { start: () => <Time minutes={shape.start!} /> })}
      </p>
      <p>
        {shape.graceOutMinutes > 0
          ? t.rich("graceOut", {
              end: () => <Time minutes={end} />,
              from: () => <Time minutes={end - shape.graceOutMinutes} />,
              minutes: shape.graceOutMinutes,
            })
          : t.rich("noGraceOut", { end: () => <Time minutes={end} /> })}
      </p>
      <p className="text-ink-muted">{t("graceIsNotTime")}</p>
    </>
  )
}

function ThresholdExplainer({ shape }: { shape: ShiftShape }) {
  const t = useTranslations("scheduling.shifts.explain")
  return (
    <>
      <p>{t("halfDay", { hours: formatHours(shape.halfDayThresholdMinutes) })}</p>
      <p>{t("fullDay", { hours: formatHours(shape.fullDayMinutes) })}</p>
      <p className="text-ink-muted">{t("absent")}</p>
    </>
  )
}

function WindowExplainer({ shape }: { shape: ShiftShape }) {
  const t = useTranslations("scheduling.shifts.explain")
  if (shape.start === null || shape.end === null || shiftDuration(shape.start, shape.end) === 0) return null
  const end = shape.start + shiftDuration(shape.start, shape.end)
  return (
    <>
      <p>
        {t.rich("window", {
          from: () => <Time minutes={shape.start! - shape.punchWindowBeforeMinutes} />,
          until: () => <Time minutes={end + shape.punchWindowAfterMinutes} />,
        })}
      </p>
      <p className="text-ink-muted">{t("windowIsNotGrace")}</p>
    </>
  )
}

function BackToShifts() {
  const t = useTranslations("scheduling.shifts")
  return (
    <Button asChild variant="ghost">
      <Link href="/setup/shifts">
        <ArrowLeftIcon aria-hidden="true" />
        {t("backToList")}
      </Link>
    </Button>
  )
}

function EditorSkeleton() {
  return (
    <div aria-hidden="true" className="flex flex-col">
      <div className="border-b border-rule bg-surface px-3 py-2">
        <Skeleton className="h-3 w-24" />
        <Skeleton className="mt-1 h-2 w-48" />
      </div>
      <div className="border-b border-rule bg-surface px-3 py-2">
        <Skeleton className="h-[176px] max-w-[960px]" />
      </div>
      <div className="flex max-w-form flex-col gap-3 px-3 py-3">
        {[0, 1, 2].map((key) => (
          <Skeleton key={key} className="h-16" />
        ))}
      </div>
    </div>
  )
}

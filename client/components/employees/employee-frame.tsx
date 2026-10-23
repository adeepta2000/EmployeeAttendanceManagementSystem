"use client"

import { CalendarClockIcon, FileTextIcon, PalmtreeIcon } from "lucide-react"
import { useTranslations } from "next-intl"
import type { ReactNode } from "react"
import { z } from "zod"

import { PageHeader } from "@/components/page-header"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { useUrlState } from "@/lib/use-url-state"
import { cn } from "@/lib/utils"

/** Screen 27's tabs, in order. */
export const EMPLOYEE_TABS = ["profile", "employment", "shift", "leave", "documents"] as const
export type EmployeeTab = (typeof EMPLOYEE_TABS)[number]

const tabSchema = z.object({ tab: z.enum(EMPLOYEE_TABS).catch("profile").default("profile") })

/** The open tab, kept in `?tab=` so a link can point straight at it. */
export function useEmployeeTab() {
  const [state, setState] = useUrlState(tabSchema)
  return [state.tab, (tab: EmployeeTab) => setState({ tab }, { replace: true })] as const
}

type Props = {
  title: ReactNode
  description?: ReactNode
  actions?: ReactNode
  tab: EmployeeTab
  onTabChange: (tab: EmployeeTab) => void
  /** Tabs that can't open yet, e.g. everything but the record itself while it is being created. */
  disabledTabs?: EmployeeTab[]
  /** Tabs with an invalid field, marked so the user can find the error. */
  tabsWithErrors?: EmployeeTab[]
  /** One element per tab: the content of the record's own tabs (profile, employment). */
  profile: ReactNode
  employment: ReactNode
  footer?: ReactNode
}

/**
 * The tabbed layout of screen 27: profile, employment, shift, leave balance, documents. The record's own
 * tabs stay mounted while hidden, so an edit spread over both keeps its values and its errors.
 */
export function EmployeeFrame({
  title,
  description,
  actions,
  tab,
  onTabChange,
  disabledTabs = [],
  tabsWithErrors = [],
  profile,
  employment,
  footer,
}: Props) {
  const t = useTranslations("employees.detail")

  return (
    <Tabs
      value={tab}
      onValueChange={(value) => onTabChange(value as EmployeeTab)}
      className="flex h-full flex-col gap-0"
    >
      <PageHeader title={title} description={description} actions={actions}>
        <TabsList variant="line" className="-mb-px h-auto gap-2 p-0">
          {EMPLOYEE_TABS.map((value) => (
            <TabsTrigger
              key={value}
              value={value}
              disabled={disabledTabs.includes(value)}
              className={cn(
                "h-5 flex-none rounded-none border-0 border-b-2 border-transparent px-0.5 text-14 text-ink-muted after:hidden",
                "data-active:border-brand data-active:text-brand data-active:shadow-none",
              )}
            >
              {t(`tabs.${value}`)}
              {tabsWithErrors.includes(value) && (
                <span className="size-0.75 rounded-full bg-danger">
                  <span className="sr-only">{t("tabHasErrors")}</span>
                </span>
              )}
            </TabsTrigger>
          ))}
        </TabsList>
      </PageHeader>

      <div className="min-h-0 flex-1 overflow-auto px-3 py-3">
        <div className="max-w-form">
          <TabsContent value="profile" forceMount className="data-[state=inactive]:hidden">
            {profile}
          </TabsContent>
          <TabsContent value="employment" forceMount className="data-[state=inactive]:hidden">
            {employment}
          </TabsContent>
          <TabsContent value="shift">
            <ComingLater icon={<CalendarClockIcon aria-hidden="true" />} title={t("shift.title")} body={t("shift.body")} />
          </TabsContent>
          <TabsContent value="leave">
            <ComingLater icon={<PalmtreeIcon aria-hidden="true" />} title={t("leave.title")} body={t("leave.body")} />
          </TabsContent>
          <TabsContent value="documents">
            <ComingLater icon={<FileTextIcon aria-hidden="true" />} title={t("documents.title")} body={t("documents.body")} />
          </TabsContent>
        </div>
      </div>

      {footer}
    </Tabs>
  )
}

/** A tab whose module isn't built yet: says what will be here and where it comes from. */
function ComingLater({ icon, title, body }: { icon: ReactNode; title: string; body: string }) {
  return (
    <div className="flex flex-col items-start gap-1 rounded-md border border-dashed border-rule bg-surface p-3 text-ink-muted [&_svg]:size-3">
      {icon}
      <h2 className="text-16 font-semibold text-ink">{title}</h2>
      <p className="text-14">{body}</p>
    </div>
  )
}

/** A labelled group of fields or values inside a tab. */
export function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="mb-3 rounded-md border border-rule bg-surface p-3">
      <h2 className="mb-2 text-16 font-semibold">{title}</h2>
      {children}
    </section>
  )
}

"use client"

import { ArrowLeftIcon, PencilIcon, UserCheckIcon, UserXIcon } from "lucide-react"
import Link from "next/link"
import { useRouter } from "next/navigation"
import { useTranslations } from "next-intl"
import { useState } from "react"

import { FormAlert } from "@/components/forms/form-alert"
import { PageHeader } from "@/components/page-header"
import { EmployeeShiftTab } from "@/components/scheduling/employee-shift-tab"
import { useSession } from "@/components/shell/session-context"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { ApiError } from "@/lib/api/client"
import { useErrorText } from "@/lib/api/errors"
import { useEmployee } from "@/lib/api/queries"
import type { Employee } from "@/lib/api/types"
import { Permissions } from "@/lib/permissions"

import { ActiveBadge } from "./active-badge"
import { EmployeeForm } from "./employee-form"
import { EmployeeFrame, useEmployeeTab } from "./employee-frame"
import { DeactivateEmployeeDialog, ReactivateEmployeeDialog } from "./employee-status-dialogs"
import { EmploymentFacts, ProfileFacts } from "./employee-view"

/** Screen 27: one employee, read-only by default, edited in place across its tabs. */
export function EmployeeDetail({ id }: { id: string }) {
  const t = useTranslations("employees.detail")
  const errorText = useErrorText()
  const query = useEmployee(id)
  const [tab, setTab] = useEmployeeTab()
  const [editing, setEditing] = useState(false)

  if (query.isPending) return <DetailSkeleton />

  if (query.isError) {
    // The API answers 404 both for a record that doesn't exist and for one outside the caller's scope.
    const notFound = query.error instanceof ApiError && query.error.status === 404
    return (
      <div className="flex flex-col gap-2 p-3">
        <FormAlert>{notFound ? t("notFound") : errorText(query.error)}</FormAlert>
        <BackToList />
      </div>
    )
  }

  const employee = query.data
  const title = (
    <span className="flex flex-wrap items-center gap-1">
      {employee.fullName}
      <ActiveBadge active={employee.isActive} />
    </span>
  )
  const description = (
    <span>
      <span className="font-mono">{employee.employeeCode}</span> · {employee.designation.name} · {employee.department.name}
    </span>
  )

  if (editing) {
    return (
      <EmployeeForm
        employee={employee}
        title={t("editTitle", { name: employee.fullName })}
        description={description}
        tab={tab === "profile" || tab === "employment" ? tab : "profile"}
        onTabChange={setTab}
        onCancel={() => setEditing(false)}
        onSaved={() => setEditing(false)}
      />
    )
  }

  return (
    <EmployeeFrame
      title={title}
      description={description}
      actions={<Actions employee={employee} onEdit={() => setEditing(true)} />}
      tab={tab}
      onTabChange={setTab}
      profile={<ProfileFacts employee={employee} />}
      employment={<EmploymentFacts employee={employee} />}
      shift={<EmployeeShiftTab employee={employee} />}
    />
  )
}

function Actions({ employee, onEdit }: { employee: Employee; onEdit: () => void }) {
  const t = useTranslations("employees.detail")
  const { can } = useSession()
  const [dialog, setDialog] = useState<"deactivate" | "reactivate" | null>(null)

  if (!can(Permissions.Employee.Write)) return null

  return (
    <>
      {employee.isActive ? (
        <Button variant="outline" onClick={() => setDialog("deactivate")}>
          <UserXIcon aria-hidden="true" />
          {t("deactivate")}
        </Button>
      ) : (
        <Button variant="outline" onClick={() => setDialog("reactivate")}>
          <UserCheckIcon aria-hidden="true" />
          {t("reactivate")}
        </Button>
      )}
      <Button onClick={onEdit}>
        <PencilIcon aria-hidden="true" />
        {t("edit")}
      </Button>
      <DeactivateEmployeeDialog
        employee={employee}
        open={dialog === "deactivate"}
        onOpenChange={(open) => setDialog(open ? "deactivate" : null)}
      />
      <ReactivateEmployeeDialog
        employee={employee}
        open={dialog === "reactivate"}
        onOpenChange={(open) => setDialog(open ? "reactivate" : null)}
      />
    </>
  )
}

/** Screen 27 in add mode. */
export function NewEmployee() {
  const t = useTranslations("employees.detail")
  const router = useRouter()
  const [tab, setTab] = useEmployeeTab()

  return (
    <EmployeeForm
      title={t("newTitle")}
      description={t("newDescription")}
      tab={tab === "employment" ? "employment" : "profile"}
      onTabChange={setTab}
      onCancel={() => router.push("/employees")}
      onSaved={(employee) => router.replace(`/employees/${employee.publicId}`)}
    />
  )
}

function BackToList() {
  const t = useTranslations("employees.detail")
  return (
    <Button asChild variant="outline" className="self-start">
      <Link href="/employees">
        <ArrowLeftIcon aria-hidden="true" />
        {t("backToList")}
      </Link>
    </Button>
  )
}

/** The header and two sections, shaped like the loaded record so nothing jumps when it arrives. */
function DetailSkeleton() {
  return (
    <div aria-busy="true" className="flex h-full flex-col">
      <PageHeader title={<Skeleton className="h-2.5 w-30" />} description={<Skeleton className="mt-0.5 h-1.5 w-40" />}>
        <div className="flex h-5 items-center gap-2">
          {Array.from({ length: 5 }, (_, i) => (
            <Skeleton key={i} className="h-1.5 w-10" />
          ))}
        </div>
      </PageHeader>
      <div className="max-w-form px-3 py-3">
        {[4, 3].map((rows, section) => (
          <div key={section} className="mb-3 rounded-md border border-rule bg-surface p-3">
            <Skeleton className="mb-2 h-2 w-16" />
            {Array.from({ length: rows }, (_, i) => (
              <div key={i} className="mb-1.5 grid grid-cols-[180px_1fr] gap-3">
                <Skeleton className="h-1.5 w-14" />
                <Skeleton className="h-1.5 w-24" />
              </div>
            ))}
          </div>
        ))}
      </div>
    </div>
  )
}

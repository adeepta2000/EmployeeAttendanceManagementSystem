"use client"

import Link from "next/link"
import { useTranslations } from "next-intl"
import type { ReactNode } from "react"

import type { Employee } from "@/lib/api/types"
import { formatDate } from "@/lib/dates"

import { ActiveBadge } from "./active-badge"
import { Section } from "./employee-frame"

/** A definition list on the 8px grid: label column, value column. */
function Facts({ items }: { items: { label: string; value: ReactNode }[] }) {
  return (
    <dl className="grid grid-cols-[minmax(0,180px)_1fr] gap-x-3 gap-y-1.5 text-14">
      {items.map((item) => (
        <div key={item.label} className="contents">
          <dt className="text-ink-muted">{item.label}</dt>
          <dd className="min-w-0 text-ink">{item.value}</dd>
        </div>
      ))}
    </dl>
  )
}

function Empty() {
  const t = useTranslations("common")
  return <span className="text-ink-muted">{t("notSet")}</span>
}

export function ProfileFacts({ employee }: { employee: Employee }) {
  const t = useTranslations("employees.fields")
  const tGender = useTranslations("employees.genders")
  const tDetail = useTranslations("employees.detail")

  return (
    <>
      <Section title={tDetail("sections.personal")}>
        <Facts
          items={[
            { label: t("firstName"), value: employee.firstName },
            { label: t("lastName"), value: employee.lastName },
            { label: t("dateOfBirth"), value: <span className="tabular">{formatDate(employee.dateOfBirth)}</span> },
            { label: t("gender"), value: tGender(employee.gender) },
          ]}
        />
      </Section>
      <Section title={tDetail("sections.contact")}>
        <Facts
          items={[
            { label: t("email"), value: employee.email ?? <Empty /> },
            { label: t("phone"), value: employee.phone ? <span className="tabular">{employee.phone}</span> : <Empty /> },
            {
              label: t("account"),
              // FR-1.3: floor workers who only punch on a device have no login.
              value: employee.hasUserAccount ? tDetail("account.yes") : tDetail("account.no"),
            },
          ]}
        />
      </Section>
    </>
  )
}

export function EmploymentFacts({ employee }: { employee: Employee }) {
  const t = useTranslations("employees.fields")
  const tTypes = useTranslations("employees.employmentTypes")
  const tDetail = useTranslations("employees.detail")

  return (
    <>
      <Section title={tDetail("sections.position")}>
        <Facts
          items={[
            { label: t("employeeCode"), value: <span className="font-mono">{employee.employeeCode}</span> },
            { label: t("employmentType"), value: tTypes(employee.employmentType) },
            { label: t("department"), value: employee.department.name },
            { label: t("designation"), value: employee.designation.name },
            { label: t("workLocation"), value: employee.workLocation.name },
            {
              label: t("reportingManager"),
              value: employee.reportingManager ? (
                <Link
                  href={`/employees/${employee.reportingManager.publicId}`}
                  className="text-brand underline-offset-4 hover:underline"
                >
                  {employee.reportingManager.fullName}
                </Link>
              ) : (
                <Empty />
              ),
            },
          ]}
        />
      </Section>
      <Section title={tDetail("sections.dates")}>
        <Facts
          items={[
            { label: t("joiningDate"), value: <span className="tabular">{formatDate(employee.joiningDate)}</span> },
            {
              label: t("confirmationDate"),
              value: employee.confirmationDate ? (
                <span className="tabular">{formatDate(employee.confirmationDate)}</span>
              ) : (
                <Empty />
              ),
            },
            {
              label: t("separationDate"),
              value: employee.separationDate ? (
                <span className="tabular">{formatDate(employee.separationDate)}</span>
              ) : (
                <Empty />
              ),
            },
            { label: t("status"), value: <ActiveBadge active={employee.isActive} /> },
          ]}
        />
      </Section>
    </>
  )
}

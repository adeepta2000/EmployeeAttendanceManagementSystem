import { z } from "zod"

import { EmploymentTypes, EmployeeSortFields, Genders } from "@/lib/api/types"

import { choice, code, name, optionalDate, optionalId, optionalText, requiredDate, requiredId } from "./fields"
import { m } from "./message"

// Lengths are the API's (Department.CodeMaxLength and friends in Attendance.Domain).
const CODE_MAX = 30
const NAME_MAX = 100

/** Mirrors EmployeeFieldRules.MinimumAgeAtJoining. */
export const MINIMUM_AGE_AT_JOINING = 14

export const departmentSchema = z.object({
  code: code(CODE_MAX),
  name: name(NAME_MAX),
  parentDepartmentId: optionalId,
  headEmployeeId: optionalId,
})

export const designationSchema = z.object({
  code: code(CODE_MAX),
  name: name(NAME_MAX),
  grade: optionalText(20),
})

export const employeeSchema = z
  .object({
    employeeCode: code(CODE_MAX),
    firstName: name(NAME_MAX),
    lastName: name(NAME_MAX),
    dateOfBirth: requiredDate,
    gender: choice(Genders),
    email: z
      .string()
      .nullish()
      .transform((value) => value?.trim() || null)
      .pipe(z.email(m("validation.email")).max(256, m("validation.tooLong", { max: 256 })).nullable()),
    phone: z
      .string()
      .nullish()
      .transform((value) => value?.trim() || null)
      .pipe(
        z
          .string()
          .max(20, m("validation.tooLong", { max: 20 }))
          .regex(/^\+?[0-9][0-9 -]{5,19}$/, m("validation.phone"))
          .nullable(),
      ),
    joiningDate: requiredDate,
    confirmationDate: optionalDate,
    employmentType: choice(EmploymentTypes),
    departmentId: requiredId,
    designationId: requiredId,
    workLocationId: requiredId,
    reportingManagerId: optionalId,
  })
  .superRefine((value, ctx) => {
    if (addYears(value.dateOfBirth, MINIMUM_AGE_AT_JOINING) > value.joiningDate) {
      ctx.addIssue({
        code: "custom",
        path: ["dateOfBirth"],
        message: m("validation.minimumAge", { years: MINIMUM_AGE_AT_JOINING }),
      })
    }
    if (value.confirmationDate && value.confirmationDate < value.joiningDate) {
      ctx.addIssue({ code: "custom", path: ["confirmationDate"], message: m("validation.confirmationBeforeJoining") })
    }
  })

export const deactivateEmployeeSchema = z.object({ separationDate: requiredDate })

// Adds whole years to a yyyy-MM-dd date as text, like DateOnly.AddYears (29 Feb becomes 28 Feb).
function addYears(date: string, years: number): string {
  const [y, mo, d] = date.split("-").map(Number)
  const year = y + years
  const leap = (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0
  const day = mo === 2 && d === 29 && !leap ? 28 : d
  return `${String(year).padStart(4, "0")}-${String(mo).padStart(2, "0")}-${String(day).padStart(2, "0")}`
}

/** `?isActive=` on the master-data lists: true, false, or absent for both. */
export const activeFilterQuerySchema = z.object({ isActive: z.enum(["true", "false"]).optional() })

/** Query string of GET /api/employees, exactly as the API binds it (EmployeeListRequest). */
export const employeeListQuerySchema = z.object({
  search: z.string().trim().max(100).optional(),
  departmentId: z.guid().optional(),
  designationId: z.guid().optional(),
  workLocationId: z.guid().optional(),
  employmentType: z.enum(EmploymentTypes).optional(),
  isActive: z.enum(["true", "false"]).optional(),
  sortBy: z.enum(EmployeeSortFields).optional(),
  sortDirection: z.enum(["Asc", "Desc"]).optional(),
  page: z.coerce.number().int().min(1).max(100_000).optional(),
  pageSize: z.coerce.number().int().min(1).max(100).optional(),
})

export type EmployeeListQuery = z.infer<typeof employeeListQuerySchema>

export const EMPLOYEE_PAGE_SIZES = [25, 50, 100] as const

/**
 * The employee list's state in the page URL (spec §4.5: filters live in the query string so a view can
 * be bookmarked and shared). Anything malformed in a hand-edited or stale link falls back to its default.
 */
export const employeeListParamsSchema = z.object({
  search: z.string().trim().max(100).catch("").default(""),
  departmentId: z.guid().optional().catch(undefined),
  designationId: z.guid().optional().catch(undefined),
  workLocationId: z.guid().optional().catch(undefined),
  employmentType: z.enum(EmploymentTypes).optional().catch(undefined),
  status: z.enum(["active", "inactive", "all"]).catch("active").default("active"),
  sortBy: z.enum(EmployeeSortFields).catch("EmployeeCode").default("EmployeeCode"),
  sortDirection: z.enum(["Asc", "Desc"]).catch("Asc").default("Asc"),
  page: z.coerce.number().int().min(1).max(100_000).catch(1).default(1),
  pageSize: z.coerce
    .number()
    .refine((size) => (EMPLOYEE_PAGE_SIZES as readonly number[]).includes(size))
    .catch(25)
    .default(25),
})

export type EmployeeListParams = z.infer<typeof employeeListParamsSchema>

export function toEmployeeListQuery(params: EmployeeListParams): EmployeeListQuery {
  return {
    search: params.search || undefined,
    departmentId: params.departmentId,
    designationId: params.designationId,
    workLocationId: params.workLocationId,
    employmentType: params.employmentType,
    isActive: params.status === "all" ? undefined : params.status === "active" ? "true" : "false",
    sortBy: params.sortBy,
    sortDirection: params.sortDirection,
    page: params.page,
    pageSize: params.pageSize,
  }
}

export type DepartmentInput = z.input<typeof departmentSchema>
export type DepartmentOutput = z.output<typeof departmentSchema>
export type DesignationInput = z.input<typeof designationSchema>
export type DesignationOutput = z.output<typeof designationSchema>
export type EmployeeInput = z.input<typeof employeeSchema>
export type EmployeeOutput = z.output<typeof employeeSchema>

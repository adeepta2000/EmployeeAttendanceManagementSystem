// Response shapes of the API (src/Attendance.Application/Organization/OrganizationDtos.cs). Records are
// identified by PublicId only; internal IDs never leave the server. Dates are `yyyy-MM-dd` (DateOnly) and
// enums travel as their names.

export const Genders = ["Male", "Female", "Other"] as const
export type Gender = (typeof Genders)[number]

export const EmploymentTypes = ["Permanent", "Contract", "Probation", "Intern"] as const
export type EmploymentType = (typeof EmploymentTypes)[number]

export const EmployeeSortFields = ["EmployeeCode", "Name", "JoiningDate", "Department", "Designation"] as const
export type EmployeeSortField = (typeof EmployeeSortFields)[number]

/** A department, designation or work location as referenced from another record. */
export type OrgUnitRef = { publicId: string; code: string; name: string }

/** An employee as referenced from another record. */
export type EmployeeRef = { publicId: string; employeeCode: string; fullName: string }

export type Department = {
  publicId: string
  code: string
  name: string
  parentDepartment: OrgUnitRef | null
  headEmployee: EmployeeRef | null
  isActive: boolean
}

export type Designation = { publicId: string; code: string; name: string; grade: string | null; isActive: boolean }

export type WorkLocation = {
  publicId: string
  code: string
  name: string
  address: string | null
  latitude: number | null
  longitude: number | null
  geofenceRadiusMeters: number | null
  isActive: boolean
}

export type EmployeeSummary = {
  publicId: string
  employeeCode: string
  firstName: string
  lastName: string
  fullName: string
  department: OrgUnitRef
  designation: OrgUnitRef
  workLocation: OrgUnitRef
  employmentType: EmploymentType
  reportingManager: EmployeeRef | null
  joiningDate: string
  isActive: boolean
}

export type Employee = EmployeeSummary & {
  dateOfBirth: string
  gender: Gender
  email: string | null
  phone: string | null
  confirmationDate: string | null
  separationDate: string | null
  /** False for floor workers who only punch on a device (FR-1.3). */
  hasUserAccount: boolean
}

export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

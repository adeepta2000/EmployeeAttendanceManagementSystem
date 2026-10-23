"use client"

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import type { DepartmentOutput, DesignationOutput, EmployeeListQuery, EmployeeOutput } from "@/lib/schemas/organization"

import { backend } from "./client"
import type { Department, Designation, Employee, EmployeeSummary, PagedResult, WorkLocation } from "./types"

// Server state for the Organization module (TanStack Query). Keys are hierarchical, so invalidating
// ["employees"] refreshes every employee list and record at once.

export const queryKeys = {
  employees: ["employees"] as const,
  employeeList: (query: EmployeeListQuery) => ["employees", "list", query] as const,
  employee: (id: string) => ["employees", "detail", id] as const,
  employeeSearch: (term: string) => ["employees", "search", term] as const,
  departments: ["departments"] as const,
  designations: ["designations"] as const,
  workLocations: ["work-locations"] as const,
}

export function useEmployeeList(query: EmployeeListQuery) {
  return useQuery({
    queryKey: queryKeys.employeeList(query),
    queryFn: ({ signal }) => backend.get<PagedResult<EmployeeSummary>>("employees", query, signal),
    // Keep the current page on screen while the next one loads, so the table never collapses.
    placeholderData: keepPreviousData,
  })
}

export function useEmployee(id: string) {
  return useQuery({
    queryKey: queryKeys.employee(id),
    queryFn: ({ signal }) => backend.get<Employee>(`employees/${id}`, undefined, signal),
  })
}

/** Active employees in the caller's scope matching a code or name, for pickers and the command palette. */
export function useEmployeeSearch(term: string, options: { enabled?: boolean; pageSize?: number } = {}) {
  const trimmed = term.trim()
  return useQuery({
    queryKey: queryKeys.employeeSearch(trimmed),
    queryFn: ({ signal }) =>
      backend.get<PagedResult<EmployeeSummary>>(
        "employees",
        { search: trimmed || undefined, isActive: true, sortBy: "Name", pageSize: options.pageSize ?? 8 },
        signal,
      ),
    enabled: options.enabled ?? true,
    placeholderData: keepPreviousData,
    staleTime: 60_000,
  })
}

export function useDepartments() {
  return useQuery({
    queryKey: queryKeys.departments,
    queryFn: ({ signal }) => backend.get<Department[]>("departments", undefined, signal),
    staleTime: 5 * 60_000,
  })
}

export function useDesignations() {
  return useQuery({
    queryKey: queryKeys.designations,
    queryFn: ({ signal }) => backend.get<Designation[]>("designations", undefined, signal),
    staleTime: 5 * 60_000,
  })
}

export function useWorkLocations() {
  return useQuery({
    queryKey: queryKeys.workLocations,
    queryFn: ({ signal }) => backend.get<WorkLocation[]>("work-locations", undefined, signal),
    staleTime: 5 * 60_000,
  })
}

export function useSaveEmployee(id?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (values: EmployeeOutput) =>
      id ? backend.put<Employee>(`employees/${id}`, values) : backend.post<Employee>("employees", values),
    onSuccess: (employee) => {
      queryClient.setQueryData(queryKeys.employee(employee.publicId), employee)
      return queryClient.invalidateQueries({ queryKey: queryKeys.employees })
    },
  })
}

export function useSetEmployeeActive(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (change: { active: true } | { active: false; separationDate: string }) =>
      change.active
        ? backend.post<Employee>(`employees/${id}/reactivate`)
        : backend.post<Employee>(`employees/${id}/deactivate`, { separationDate: change.separationDate }),
    onSuccess: (employee) => {
      queryClient.setQueryData(queryKeys.employee(id), employee)
      // Department heads and reporting managers shown elsewhere may have changed too.
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.employees }),
        queryClient.invalidateQueries({ queryKey: queryKeys.departments }),
      ])
    },
  })
}

function useMasterDataMutations<TInput, TResult>(resource: "departments" | "designations") {
  const queryClient = useQueryClient()
  // Employee rows show department and designation names, so they refresh too.
  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: [resource] }),
      queryClient.invalidateQueries({ queryKey: queryKeys.employees }),
    ])

  const save = useMutation({
    mutationFn: ({ id, values }: { id?: string; values: TInput }) =>
      id ? backend.put<TResult>(`${resource}/${id}`, values) : backend.post<TResult>(resource, values),
    onSuccess: invalidate,
  })

  const setActive = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) =>
      backend.post<TResult>(`${resource}/${id}/${active ? "activate" : "deactivate"}`),
    onSuccess: invalidate,
  })

  return { save, setActive }
}

export const useDepartmentMutations = () => useMasterDataMutations<DepartmentOutput, Department>("departments")
export const useDesignationMutations = () => useMasterDataMutations<DesignationOutput, Designation>("designations")

"use client"

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query"

import type {
  AssignRosterOutput,
  BulkRosterOutput,
  ClearRosterOutput,
  EmployeeScheduleOutput,
  HolidayOutput,
  ShiftOutput,
  TeamCalendarQuery,
  WeeklyOffPatternOutput,
} from "@/lib/schemas/scheduling"

import { backend } from "./client"
import type {
  BulkRosterResult,
  CalendarDay,
  ClearRosterResult,
  EmployeeSchedule,
  Holiday,
  Shift,
  TeamCalendarRow,
  WeeklyOffPattern,
} from "./scheduling-types"
import type { PagedResult } from "./types"

// Server state for the Scheduling module. Every roster write invalidates ["team-calendar"], so the grid
// shows what the API resolved rather than what the browser guessed.

export const schedulingKeys = {
  shifts: ["shifts"] as const,
  shift: (id: string) => ["shifts", "detail", id] as const,
  patterns: ["weekly-off-patterns"] as const,
  holidays: ["holidays"] as const,
  holidayYear: (year: number, workLocationId?: string) => ["holidays", year, workLocationId ?? "all"] as const,
  teamCalendar: ["team-calendar"] as const,
  teamCalendarPage: (query: TeamCalendarQuery) => ["team-calendar", query] as const,
  // Under ["team-calendar"] too, so every roster write refreshes an open Shift tab as well.
  employeeCalendar: (id: string) => ["team-calendar", "employee", id] as const,
  employeeCalendarRange: (id: string, fromDate: string, toDate: string) =>
    ["team-calendar", "employee", id, fromDate, toDate] as const,
  employeeSchedule: (id: string) => ["employee-schedule", id] as const,
}

export function useShifts() {
  return useQuery({
    queryKey: schedulingKeys.shifts,
    queryFn: ({ signal }) => backend.get<Shift[]>("shifts", undefined, signal),
    staleTime: 5 * 60_000,
  })
}

export function useShift(id: string) {
  return useQuery({
    queryKey: schedulingKeys.shift(id),
    queryFn: ({ signal }) => backend.get<Shift>(`shifts/${id}`, undefined, signal),
  })
}

export function useShiftMutations() {
  const queryClient = useQueryClient()
  // Roster cells show shift codes, so the grid refreshes with the list.
  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: schedulingKeys.shifts }),
      queryClient.invalidateQueries({ queryKey: schedulingKeys.teamCalendar }),
    ])

  const save = useMutation({
    mutationFn: ({ id, values }: { id?: string; values: ShiftOutput }) =>
      id ? backend.put<Shift>(`shifts/${id}`, values) : backend.post<Shift>("shifts", values),
    onSuccess: (shift) => {
      queryClient.setQueryData(schedulingKeys.shift(shift.publicId), shift)
      return invalidate()
    },
  })

  const setActive = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) =>
      backend.post<Shift>(`shifts/${id}/${active ? "activate" : "deactivate"}`),
    onSuccess: invalidate,
  })

  return { save, setActive }
}

export function useWeeklyOffPatterns() {
  return useQuery({
    queryKey: schedulingKeys.patterns,
    queryFn: ({ signal }) => backend.get<WeeklyOffPattern[]>("weekly-off-patterns", undefined, signal),
    staleTime: 5 * 60_000,
  })
}

export function useWeeklyOffPatternMutations() {
  const queryClient = useQueryClient()
  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: schedulingKeys.patterns }),
      queryClient.invalidateQueries({ queryKey: schedulingKeys.teamCalendar }),
    ])

  const create = useMutation({
    mutationFn: (values: WeeklyOffPatternOutput) => backend.post<WeeklyOffPattern>("weekly-off-patterns", values),
    onSuccess: invalidate,
  })

  const rename = useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) =>
      backend.put<WeeklyOffPattern>(`weekly-off-patterns/${id}`, { name }),
    onSuccess: invalidate,
  })

  const setActive = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) =>
      backend.post<WeeklyOffPattern>(`weekly-off-patterns/${id}/${active ? "activate" : "deactivate"}`),
    onSuccess: invalidate,
  })

  return { create, rename, setActive }
}

export function useHolidays(year: number, workLocationId?: string) {
  return useQuery({
    queryKey: schedulingKeys.holidayYear(year, workLocationId),
    queryFn: ({ signal }) => backend.get<Holiday[]>("holidays", { year, workLocationId }, signal),
    placeholderData: keepPreviousData,
  })
}

export function useHolidayMutations() {
  const queryClient = useQueryClient()
  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: schedulingKeys.holidays }),
      queryClient.invalidateQueries({ queryKey: schedulingKeys.teamCalendar }),
    ])

  const save = useMutation({
    mutationFn: ({ id, values }: { id?: string; values: HolidayOutput }) =>
      id ? backend.put<Holiday>(`holidays/${id}`, values) : backend.post<Holiday>("holidays", values),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => backend.delete(`holidays/${id}`),
    onSuccess: invalidate,
  })

  return { save, remove }
}

export function useTeamCalendar(query: TeamCalendarQuery) {
  return useQuery({
    queryKey: schedulingKeys.teamCalendarPage(query),
    queryFn: ({ signal }) => backend.get<PagedResult<TeamCalendarRow>>("rosters/calendar", query, signal),
    // The previous period stays on screen while the next loads, so the grid never collapses.
    placeholderData: keepPreviousData,
  })
}

export function useRosterMutations() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: schedulingKeys.teamCalendar })

  const assign = useMutation({
    mutationFn: (values: AssignRosterOutput) => backend.post<BulkRosterResult>("rosters/assign", values),
    onSuccess: invalidate,
  })

  const clear = useMutation({
    mutationFn: (values: ClearRosterOutput) => backend.post<ClearRosterResult>("rosters/clear", values),
    onSuccess: invalidate,
  })

  const bulk = useMutation({
    mutationFn: (values: BulkRosterOutput) => backend.post<BulkRosterResult>("rosters/bulk", values),
    onSuccess: invalidate,
  })

  return { assign, clear, bulk }
}

export function useEmployeeSchedule(id: string) {
  return useQuery({
    queryKey: schedulingKeys.employeeSchedule(id),
    queryFn: ({ signal }) => backend.get<EmployeeSchedule>(`employees/${id}/schedule`, undefined, signal),
  })
}

export function useEmployeeCalendar(id: string, fromDate: string, toDate: string) {
  return useQuery({
    queryKey: schedulingKeys.employeeCalendarRange(id, fromDate, toDate),
    queryFn: ({ signal }) => backend.get<CalendarDay[]>(`employees/${id}/calendar`, { fromDate, toDate }, signal),
    placeholderData: keepPreviousData,
  })
}

export function useSetEmployeeSchedule(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (values: EmployeeScheduleOutput) => backend.put<EmployeeSchedule>(`employees/${id}/schedule`, values),
    onSuccess: (schedule) => {
      queryClient.setQueryData(schedulingKeys.employeeSchedule(id), schedule)
      // Every calendar that resolves this employee's days: their own and the team roster's.
      return queryClient.invalidateQueries({ queryKey: schedulingKeys.teamCalendar })
    },
  })
}

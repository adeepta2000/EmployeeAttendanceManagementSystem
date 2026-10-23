/**
 * Attendance statuses (spec §4.2). Each has a letter code so the muster roll reads in greyscale print and
 * for colour-blind users: colour never carries the meaning alone. The classes use the status tokens in
 * app/globals.css: a soft tint behind the letter, never a solid block (§4.4).
 */
export const AttendanceStatuses = {
  present: { code: "P", text: "text-present", tint: "bg-present/10", border: "border-present/30" },
  absent: { code: "A", text: "text-absent", tint: "bg-absent/10", border: "border-absent/30" },
  late: { code: "L", text: "text-late", tint: "bg-late/10", border: "border-late/30" },
  leave: { code: "LV", text: "text-leave", tint: "bg-leave/10", border: "border-leave/30" },
  weeklyOff: { code: "WO", text: "text-weekly-off", tint: "bg-weekly-off/10", border: "border-weekly-off/30" },
  holiday: { code: "H", text: "text-holiday", tint: "bg-holiday/10", border: "border-holiday/30" },
  overtime: { code: "OT", text: "text-overtime", tint: "bg-overtime/10", border: "border-overtime/30" },
} as const

export type AttendanceStatus = keyof typeof AttendanceStatuses

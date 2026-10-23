import { z } from "zod"

import { m } from "./message"

// Field rules shared by the forms (via zodResolver) and the Route Handlers (server-side validation).
// They mirror the API's FluentValidation rules, so most mistakes are caught before a round trip; the API
// still validates everything.
//
// Every schema is idempotent: its output is valid input. The browser sends the parsed form values, and
// the Route Handler parses them again with the same schema.

export const required = m("validation.required")

/** Business codes such as SEW or NW-001 (the API's OrgCodes rule). */
export const code = (max: number) =>
  z
    .string()
    .trim()
    .min(1, required)
    .max(max, m("validation.code", { max }))
    .regex(/^[A-Za-z0-9][A-Za-z0-9_./-]*$/, m("validation.code", { max }))

export const name = (max: number) =>
  z
    .string()
    .trim()
    .min(1, required)
    .max(max, m("validation.tooLong", { max }))

/** Optional free text: blank becomes null. */
export const optionalText = (max: number) =>
  z
    .string()
    .nullish()
    .transform((value) => value?.trim() || null)
    .pipe(z.string().max(max, m("validation.tooLong", { max })).nullable())

/** The API's floor for every date, which catches typos such as 0024 for 2024. */
export const EARLIEST_DATE = "1900-01-01"

/** A calendar date as the API's DateOnly: `yyyy-MM-dd`. */
export const requiredDate = z
  .string()
  .trim()
  .min(1, required)
  .pipe(z.iso.date(m("validation.date")))
  .refine((value) => value >= EARLIEST_DATE, m("validation.dateTooEarly"))

export const optionalDate = z
  .string()
  .nullish()
  .transform((value) => value?.trim() || null)
  .pipe(
    z.iso
      .date(m("validation.date"))
      .refine((value) => value >= EARLIEST_DATE, m("validation.dateTooEarly"))
      .nullable(),
  )

/** A PublicId picked from a list. Blank means nothing was chosen. */
export const requiredId = z.string().trim().min(1, m("validation.choose")).pipe(z.guid(m("validation.choose")))

export const optionalId = z
  .string()
  .nullish()
  .transform((value) => value?.trim() || null)
  .pipe(z.guid(m("validation.choose")).nullable())

/** One of a fixed set of names, e.g. an enum the API sends as strings. Blank means nothing was chosen. */
export const choice = <const T extends readonly [string, ...string[]]>(values: T) =>
  z
    .string()
    .refine((value) => (values as readonly string[]).includes(value), m("validation.choose"))
    .transform((value) => value as T[number])

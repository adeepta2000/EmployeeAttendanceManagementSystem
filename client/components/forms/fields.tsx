"use client"

import { useId, type ComponentProps, type ReactNode } from "react"
import { Controller, type Control, type FieldPath, type FieldValues } from "react-hook-form"

import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { useMessageText } from "@/lib/api/errors"
import { cn } from "@/lib/utils"

// Form fields wired to React Hook Form. Each links its description and error to the control with
// aria-describedby, and the error is a live region (role="alert") so screen readers announce it (§4.7).

type FieldProps<T extends FieldValues> = {
  control: Control<T>
  name: FieldPath<T>
  label: string
  description?: ReactNode
  required?: boolean
  disabled?: boolean
  className?: string
}

type ControlProps = {
  id: string
  "aria-invalid": boolean
  "aria-describedby": string | undefined
  "aria-required": boolean | undefined
}

function FieldShell<T extends FieldValues>({
  control,
  name,
  label,
  description,
  required,
  className,
  children,
}: FieldProps<T> & {
  children: (field: { value: unknown; onChange: (value: unknown) => void; onBlur: () => void; ref: (el: unknown) => void; name: string }, props: ControlProps) => ReactNode
}) {
  const id = useId()
  const messageText = useMessageText()

  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => {
        const error = messageText(fieldState.error?.message)
        const describedBy = [description ? `${id}-description` : null, error ? `${id}-error` : null]
          .filter(Boolean)
          .join(" ")
        return (
          <Field data-invalid={!!error || undefined} className={className}>
            <FieldLabel htmlFor={id}>
              {label}
              {required && (
                <span aria-hidden="true" className="text-danger">
                  *
                </span>
              )}
            </FieldLabel>
            {children(field, {
              id,
              "aria-invalid": !!error,
              "aria-describedby": describedBy || undefined,
              "aria-required": required || undefined,
            })}
            {description && <FieldDescription id={`${id}-description`}>{description}</FieldDescription>}
            {error && <FieldError id={`${id}-error`}>{error}</FieldError>}
          </Field>
        )
      }}
    />
  )
}

export function TextField<T extends FieldValues>({
  type = "text",
  autoComplete,
  inputMode,
  placeholder,
  mono,
  autoFocus,
  ...props
}: FieldProps<T> & {
  type?: "text" | "email" | "password" | "tel" | "date" | "search"
  autoComplete?: string
  inputMode?: ComponentProps<"input">["inputMode"]
  placeholder?: string
  /** Employee codes and other identifiers render in JetBrains Mono (§4.2). */
  mono?: boolean
  autoFocus?: boolean
}) {
  return (
    <FieldShell {...props}>
      {(field, control) => (
        <Input
          {...control}
          ref={field.ref}
          name={field.name}
          value={(field.value as string | null | undefined) ?? ""}
          onChange={(event) => field.onChange(event.target.value)}
          onBlur={field.onBlur}
          type={type}
          autoComplete={autoComplete}
          inputMode={inputMode}
          placeholder={placeholder}
          disabled={props.disabled}
          autoFocus={autoFocus}
          className={cn(mono && "font-mono")}
        />
      )}
    </FieldShell>
  )
}

export type Option = { value: string; label: string; disabled?: boolean }

/** The value an optional select uses for "nothing chosen"; Radix doesn't allow an empty-string item. */
const NONE = "__none__"

export function SelectField<T extends FieldValues>({
  options,
  placeholder,
  noneLabel,
  ...props
}: FieldProps<T> & {
  options: Option[]
  placeholder?: string
  /** For an optional field: the label of the "none" entry, which clears the value. */
  noneLabel?: string
}) {
  return (
    <FieldShell {...props}>
      {(field, control) => (
        <Select
          name={field.name}
          value={(field.value as string | null | undefined) || (noneLabel ? NONE : "")}
          onValueChange={(value) => field.onChange(value === NONE ? "" : value)}
          disabled={props.disabled}
        >
          <SelectTrigger {...control} ref={field.ref as never} onBlur={field.onBlur} className="w-full">
            {/* Explicit text: Radix only fills SelectValue in after its items mount, which leaves a blank trigger. */}
            <SelectValue placeholder={placeholder}>
              {options.find((option) => option.value === field.value)?.label ?? (field.value ? undefined : noneLabel)}
            </SelectValue>
          </SelectTrigger>
          <SelectContent position="popper">
            {noneLabel && <SelectItem value={NONE}>{noneLabel}</SelectItem>}
            {options.map((option) => (
              <SelectItem key={option.value} value={option.value} disabled={option.disabled}>
                {option.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      )}
    </FieldShell>
  )
}

/** Any other control (e.g. the employee picker) inside the same label, description and error frame. */
export function CustomField<T extends FieldValues>({
  render,
  ...props
}: FieldProps<T> & {
  render: (field: { value: unknown; onChange: (value: unknown) => void; onBlur: () => void }, control: ControlProps) => ReactNode
}) {
  return <FieldShell {...props}>{(field, control) => render(field, control)}</FieldShell>
}

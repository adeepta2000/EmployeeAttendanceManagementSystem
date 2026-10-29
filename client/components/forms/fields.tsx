"use client"

import { useId, type ChangeEvent, type ComponentProps, type ReactNode } from "react"
import { Controller, type Control, type FieldPath, type FieldValues } from "react-hook-form"

import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { InputGroup, InputGroupAddon, InputGroupInput, InputGroupText } from "@/components/ui/input-group"
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
  suffix,
  ...props
}: FieldProps<T> & {
  type?: "text" | "email" | "password" | "tel" | "date" | "time" | "search"
  autoComplete?: string
  inputMode?: ComponentProps<"input">["inputMode"]
  placeholder?: string
  /** Employee codes, times, durations and other identifiers render in JetBrains Mono (§4.2). */
  mono?: boolean
  autoFocus?: boolean
  /** A unit shown inside the input's right edge, e.g. "min". */
  suffix?: ReactNode
}) {
  return (
    <FieldShell {...props}>
      {(field, control) => {
        const inputProps = {
          ...control,
          ref: field.ref,
          name: field.name,
          value: String((field.value as string | number | null | undefined) ?? ""),
          onChange: (event: ChangeEvent<HTMLInputElement>) => field.onChange(event.target.value),
          onBlur: field.onBlur,
          type,
          autoComplete,
          inputMode,
          placeholder,
          disabled: props.disabled,
          autoFocus,
          className: cn(mono && "font-mono"),
        }
        if (!suffix) return <Input {...inputProps} />
        return (
          <InputGroup>
            <InputGroupInput {...inputProps} />
            <InputGroupAddon align="inline-end">
              <InputGroupText className="text-13 font-normal">{suffix}</InputGroupText>
            </InputGroupAddon>
          </InputGroup>
        )
      }}
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
  valueAs,
  ...props
}: FieldProps<T> & {
  options: Option[]
  placeholder?: string
  /** For an optional field: the label of the "none" entry, which clears the value. */
  noneLabel?: string
  /** For a field that isn't text, e.g. a nullable boolean: how an option maps to the field's value and back. */
  valueAs?: { toField: (option: string) => unknown; fromField: (value: unknown) => string }
}) {
  return (
    <FieldShell {...props}>
      {(field, control) => {
        const selected = valueAs ? valueAs.fromField(field.value) : (field.value as string | null | undefined)
        return (
          <Select
            name={field.name}
            value={selected || (noneLabel ? NONE : "")}
            onValueChange={(value) => field.onChange(valueAs ? valueAs.toField(value) : value === NONE ? "" : value)}
            disabled={props.disabled}
          >
            <SelectTrigger {...control} ref={field.ref as never} onBlur={field.onBlur} className="w-full">
              {/* Explicit text: Radix only fills SelectValue in after its items mount, which leaves a blank trigger. */}
              <SelectValue placeholder={placeholder}>
                {options.find((option) => option.value === selected)?.label ?? (selected ? undefined : noneLabel)}
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
        )
      }}
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

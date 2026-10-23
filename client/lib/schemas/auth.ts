import { z } from "zod"

import { required } from "./fields"
import { m } from "./message"

// Limits from the API: User.EmailMaxLength and AuthInputLimits.PasswordMaxLength.
const EMAIL_MAX = 256
const PASSWORD_MAX = 256

/** New passwords; the same floor the demo accounts use (DemoDataOptions.PasswordMinLength). */
export const PASSWORD_MIN = 12

const email = z
  .string()
  .trim()
  .min(1, required)
  .max(EMAIL_MAX, m("validation.tooLong", { max: EMAIL_MAX }))
  .pipe(z.email(m("validation.email")))

const newPassword = z
  .string()
  .min(PASSWORD_MIN, m("validation.passwordLength", { min: PASSWORD_MIN }))
  .max(PASSWORD_MAX, m("validation.tooLong", { max: PASSWORD_MAX }))

// Login doesn't check the password policy: the policy may have changed since the password was set.
export const loginSchema = z.object({
  email,
  password: z.string().min(1, required).max(PASSWORD_MAX, m("validation.tooLong", { max: PASSWORD_MAX })),
})

export const forgotPasswordSchema = z.object({ email })

const confirmed = <T extends { newPassword: string; confirmPassword: string }>(value: T, ctx: z.RefinementCtx) => {
  if (value.newPassword !== value.confirmPassword) {
    ctx.addIssue({ code: "custom", path: ["confirmPassword"], message: m("validation.passwordMismatch") })
  }
}

export const resetPasswordSchema = z
  .object({
    // The one-time token from the emailed link; opaque to the client.
    token: z.string().trim().min(1, m("auth.reset.errors.missingToken")).max(512),
    newPassword,
    confirmPassword: z.string(),
  })
  .superRefine(confirmed)

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, required).max(PASSWORD_MAX),
    newPassword,
    confirmPassword: z.string(),
  })
  .superRefine((value, ctx) => {
    confirmed(value, ctx)
    if (value.newPassword && value.newPassword === value.currentPassword) {
      ctx.addIssue({ code: "custom", path: ["newPassword"], message: m("validation.passwordSame") })
    }
  })

export type LoginInput = z.input<typeof loginSchema>
export type ForgotPasswordInput = z.input<typeof forgotPasswordSchema>
export type ResetPasswordInput = z.input<typeof resetPasswordSchema>
export type ChangePasswordInput = z.input<typeof changePasswordSchema>

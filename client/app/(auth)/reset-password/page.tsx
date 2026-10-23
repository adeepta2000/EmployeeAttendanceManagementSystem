import type { Metadata } from "next"
import { getTranslations } from "next-intl/server"

import { ResetPasswordForm } from "./reset-password-form"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("auth.reset")
  return { title: t("title") }
}

/**
 * Screen 3. The reset link carries its token in the fragment (`/reset-password#token=…`), which browsers
 * never send to a server, so it stays out of access logs and Referer headers.
 */
export default function ResetPasswordPage() {
  return <ResetPasswordForm />
}

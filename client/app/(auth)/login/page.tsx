import type { Metadata } from "next"
import { getTranslations } from "next-intl/server"

import { LoginForm } from "./login-form"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("auth.login")
  return { title: t("title") }
}

const REASONS = ["expired", "signed-out", "unavailable", "password-changed"] as const
type Reason = (typeof REASONS)[number]

/** Screen 1. `?reason=` explains why the user is here; `?next=` is where to go after signing in. */
export default async function LoginPage({ searchParams }: PageProps<"/login">) {
  const params = await searchParams
  const reason = typeof params.reason === "string" && (REASONS as readonly string[]).includes(params.reason)
    ? (params.reason as Reason)
    : null
  const next = typeof params.next === "string" ? params.next : null

  return <LoginForm reason={reason} next={next} />
}

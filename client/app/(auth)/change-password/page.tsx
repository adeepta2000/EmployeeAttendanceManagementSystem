import type { Metadata } from "next"
import { cookies } from "next/headers"
import { getTranslations } from "next-intl/server"

import { CookieNames } from "@/lib/bff/cookies"

import { ChangePasswordForm } from "./change-password-form"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("auth.change")
  return { title: t("title") }
}

/**
 * Screen 4. proxy.ts sends a user here, and keeps them here, while their account must change its
 * password (first sign-in, or after an admin reset); anyone signed in can also come here from the user menu.
 */
export default async function ChangePasswordPage({ searchParams }: PageProps<"/change-password">) {
  const forced = (await cookies()).has(CookieNames.mustChangePassword)
  const { next } = await searchParams
  return <ChangePasswordForm forced={forced} next={typeof next === "string" ? next : null} />
}

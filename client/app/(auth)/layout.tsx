import { getTranslations } from "next-intl/server"
import type { ReactNode } from "react"

import { BrandMark } from "@/components/brand-mark"

/** Screens 1 to 4: one narrow column on the canvas, usable from 360px phones up. */
export default async function AuthLayout({ children }: { children: ReactNode }) {
  const t = await getTranslations("app")
  return (
    <div className="flex min-h-dvh flex-col items-center px-2 py-6 sm:py-10">
      <div className="w-full max-w-[400px]">
        <BrandMark name={t("name")} className="mb-3" />
        <main className="rounded-md border border-rule bg-surface p-3 shadow-xs">{children}</main>
        <p className="mt-2 text-12 text-ink-muted">{t("company")}</p>
      </div>
    </div>
  )
}

import { MonitorSmartphoneIcon } from "lucide-react"
import { getTranslations } from "next-intl/server"
import type { ReactNode } from "react"

/**
 * Setup and master-data screens are desktop only (§4.6). Below 900px (the `desk` breakpoint) they say so
 * plainly instead of pretending a wide form, grid or table works on a phone. Pure CSS, so nothing flips
 * after hydration.
 */
export async function DesktopOnly({ children }: { children: ReactNode }) {
  const t = await getTranslations("shell.desktopOnly")
  return (
    <>
      <div className="flex flex-col items-start gap-1 p-3 desk:hidden">
        <MonitorSmartphoneIcon aria-hidden="true" className="size-4 text-ink-muted" />
        <h1 className="text-16 font-semibold">{t("title")}</h1>
        <p className="text-14 text-ink-muted">{t("body")}</p>
      </div>
      <div className="hidden h-full desk:block">{children}</div>
    </>
  )
}

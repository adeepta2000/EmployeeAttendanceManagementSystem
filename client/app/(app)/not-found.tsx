import Link from "next/link"
import { getTranslations } from "next-intl/server"

import { Button } from "@/components/ui/button"

export default async function NotFound() {
  const t = await getTranslations("notFound")
  return (
    <div className="flex flex-col items-start gap-1 p-3">
      <h1 className="text-20 font-semibold">{t("title")}</h1>
      <p className="text-14 text-ink-muted">{t("body")}</p>
      <Button asChild variant="outline" className="mt-1">
        <Link href="/">{t("home")}</Link>
      </Button>
    </div>
  )
}

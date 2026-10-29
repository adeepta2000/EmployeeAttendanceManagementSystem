import type { Metadata } from "next"
import { notFound } from "next/navigation"
import { getTranslations } from "next-intl/server"
import { z } from "zod"

import { DesktopOnly } from "@/components/desktop-only"
import { ShiftDetail } from "@/components/scheduling/shift-editor"

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("scheduling.shifts")
  return { title: t("pageTitle") }
}

const publicId = z.guid()

/** Screen 29, one shift. Records are addressed by PublicId only ("Public keys"). */
export default async function ShiftPage({ params }: PageProps<"/setup/shifts/[publicId]">) {
  const { publicId: id } = await params
  if (!publicId.safeParse(id).success) notFound()

  return (
    <DesktopOnly>
      <ShiftDetail id={id} />
    </DesktopOnly>
  )
}

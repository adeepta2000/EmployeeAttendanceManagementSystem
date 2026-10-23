import type { ReactNode } from "react"

import { DesktopOnly } from "@/components/desktop-only"
import { OrgStructureHeader } from "@/components/org/org-structure"

/** Screen 28: departments and designations under one header, each list at its own URL. */
export default function OrgStructureLayout({ children }: { children: ReactNode }) {
  return (
    <DesktopOnly>
      <div className="flex h-full flex-col">
        <OrgStructureHeader />
        {children}
      </div>
    </DesktopOnly>
  )
}

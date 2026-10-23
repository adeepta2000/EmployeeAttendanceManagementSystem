"use client"

import Link from "next/link"
import { usePathname } from "next/navigation"
import { useTranslations } from "next-intl"

import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip"
import { isActive, type NavGroup, type NavItem } from "@/lib/nav"
import { cn } from "@/lib/utils"

import { navIcons } from "./nav-icons"

/** Groups that are a single screen don't get a heading. */
const UNLABELLED = new Set(["home", "approvals", "reports"])

type Props = {
  groups: NavGroup[]
  /** Desktop: 64px icon rail instead of the 224px menu (§4.3). */
  collapsed: boolean
  /** Below the md breakpoint the sidebar is an overlay, open or closed. */
  mobileOpen: boolean
  onNavigate: () => void
}

export function Sidebar({ groups, collapsed, mobileOpen, onNavigate }: Props) {
  const t = useTranslations("nav")
  const pathname = usePathname()

  return (
    <nav
      id="app-sidebar"
      aria-label={t("label")}
      data-collapsed={collapsed || undefined}
      className={cn(
        "flex shrink-0 flex-col border-r border-rule bg-surface",
        // Phone and tablet: an overlay under the top bar.
        "fixed inset-y-0 top-topbar left-0 z-40 w-sidebar shadow-lg md:static md:z-auto md:shadow-none",
        mobileOpen ? "flex" : "hidden md:flex",
        // Desktop: the width animates, which explains the change (§4.5).
        "md:transition-[width] md:duration-150 md:ease-out",
        collapsed && "md:w-sidebar-collapsed",
      )}
    >
      <div className="flex-1 overflow-x-hidden overflow-y-auto py-1">
        {groups.map((group) => (
          <div key={group.id} className="px-1 py-0.5">
            {!UNLABELLED.has(group.id) &&
              (collapsed ? (
                <hr className="mx-1 my-0.5 hidden border-rule md:block" />
              ) : null)}
            {!UNLABELLED.has(group.id) && (
              <div
                className={cn(
                  "px-1 pt-1 pb-0.5 text-12 font-medium tracking-wide text-ink-muted uppercase",
                  collapsed && "md:sr-only",
                )}
              >
                {t(`groups.${group.id}`)}
              </div>
            )}
            <ul className="flex flex-col gap-0.25">
              {group.items.map((item) => (
                <li key={item.id}>
                  <NavEntry item={item} active={isActive(item, pathname)} collapsed={collapsed} onNavigate={onNavigate} />
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </nav>
  )
}

function NavEntry({
  item,
  active,
  collapsed,
  onNavigate,
}: {
  item: NavItem
  active: boolean
  collapsed: boolean
  onNavigate: () => void
}) {
  const t = useTranslations("nav")
  const Icon = navIcons[item.id]
  const label = t(`items.${item.id}`)

  const content = (
    <>
      {Icon && <Icon aria-hidden="true" className="size-2 shrink-0" />}
      <span className={cn("min-w-0 flex-1 truncate", collapsed && "md:sr-only")}>{label}</span>
      {!item.available && (
        <span className={cn("text-12 text-ink-muted", collapsed && "md:hidden")}>{t("soon")}</span>
      )}
    </>
  )

  const className = cn(
    "flex h-4 items-center gap-1 rounded-md px-1 text-14",
    collapsed && "md:justify-center md:px-0",
    item.available
      ? active
        ? "bg-brand-soft font-medium text-brand"
        : "text-ink hover:bg-canvas"
      : "cursor-not-allowed text-ink-muted/70",
  )

  const entry = item.available ? (
    <Link href={item.href} aria-current={active ? "page" : undefined} className={className} onClick={onNavigate}>
      {content}
    </Link>
  ) : (
    // Not a link: the screen arrives in a later phase. aria-disabled keeps it discoverable to screen readers.
    <span role="link" aria-disabled="true" className={className} tabIndex={collapsed ? 0 : undefined}>
      {content}
    </span>
  )

  if (!collapsed) return entry
  return (
    <Tooltip>
      <TooltipTrigger asChild>{entry}</TooltipTrigger>
      <TooltipContent side="right" className="hidden md:inline-flex">
        {item.available ? label : t("soonLabel", { label })}
      </TooltipContent>
    </Tooltip>
  )
}

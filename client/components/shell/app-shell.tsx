"use client"

import { PanelLeftIcon, SearchIcon } from "lucide-react"
import Link from "next/link"
import { useTranslations } from "next-intl"
import { Suspense, useEffect, useState, type ReactNode } from "react"

import { BrandMark } from "@/components/brand-mark"
import { Button } from "@/components/ui/button"
import { Kbd, KbdGroup } from "@/components/ui/kbd"
import { PreferenceCookies, writePreferenceCookie } from "@/lib/cookie-names"
import type { NavGroup } from "@/lib/nav"

import { CommandPalette } from "./command-palette"
import { Sidebar } from "./sidebar"
import { UserMenu, UserMenuFallback, type ShellUser } from "./user-menu"

type Props = {
  groups: NavGroup[]
  initialCollapsed: boolean
  canSearchEmployees: boolean
  userPromise: Promise<ShellUser>
  children: ReactNode
}

/**
 * The application shell (§4.3): a 56px top bar with the global search and the user menu over a 224px
 * sidebar that collapses to a 64px icon rail. The collapsed state lives in a cookie, so the server renders
 * it right the first time and nothing jumps on load.
 */
export function AppShell({ groups, initialCollapsed, canSearchEmployees, userPromise, children }: Props) {
  const t = useTranslations("shell")
  const [collapsed, setCollapsed] = useState(initialCollapsed)
  const [mobileOpen, setMobileOpen] = useState(false)
  const [searchOpen, setSearchOpen] = useState(false)
  const [isMac, setIsMac] = useState(false)

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- the platform is only known in the browser
    setIsMac(/Mac|iPhone|iPad/.test(navigator.platform))
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault()
        setSearchOpen((open) => !open)
      }
    }
    window.addEventListener("keydown", onKeyDown)
    return () => window.removeEventListener("keydown", onKeyDown)
  }, [])

  const toggleSidebar = () => {
    if (window.matchMedia("(min-width: 768px)").matches) {
      const next = !collapsed
      setCollapsed(next)
      writePreferenceCookie(PreferenceCookies.sidebar, next ? "collapsed" : "expanded")
    } else {
      setMobileOpen((open) => !open)
    }
  }

  const expanded = !collapsed

  return (
    <div className="flex h-dvh flex-col">
      <a
        href="#main"
        className="sr-only z-50 rounded-md bg-surface px-1.5 py-1 text-14 focus:not-sr-only focus:fixed focus:top-1 focus:left-1"
      >
        {t("skipToContent")}
      </a>

      <header className="flex h-topbar shrink-0 items-center gap-1 border-b border-rule bg-surface px-1.5">
        <Button
          variant="ghost"
          size="icon"
          onClick={toggleSidebar}
          aria-controls="app-sidebar"
          aria-expanded={mobileOpen || expanded}
          aria-label={expanded ? t("collapseSidebar") : t("expandSidebar")}
        >
          <PanelLeftIcon aria-hidden="true" />
        </Button>
        <Link href="/" className="mr-2 rounded-md">
          <BrandMark name={t("productName")} />
        </Link>

        <button
          type="button"
          onClick={() => setSearchOpen(true)}
          aria-keyshortcuts={isMac ? "Meta+K" : "Control+K"}
          className="mx-auto flex h-4 w-full max-w-[480px] min-w-0 items-center gap-1 rounded-md border border-rule bg-canvas px-1 text-left text-14 text-ink-muted hover:border-ink-muted/40"
        >
          <SearchIcon aria-hidden="true" className="size-2 shrink-0" />
          <span className="min-w-0 flex-1 truncate">{t("search.trigger")}</span>
          <KbdGroup className="hidden sm:inline-flex">
            <Kbd>{isMac ? "⌘" : "Ctrl"}</Kbd>
            <Kbd>K</Kbd>
          </KbdGroup>
        </button>

        <div className="ml-2 flex items-center">
          <Suspense fallback={<UserMenuFallback />}>
            <UserMenu userPromise={userPromise} />
          </Suspense>
        </div>
      </header>

      <div className="relative flex min-h-0 flex-1">
        <Sidebar
          groups={groups}
          collapsed={collapsed}
          mobileOpen={mobileOpen}
          onNavigate={() => setMobileOpen(false)}
        />
        {mobileOpen && (
          <div
            aria-hidden="true"
            className="fixed inset-0 top-topbar z-30 bg-ink/20 md:hidden"
            onClick={() => setMobileOpen(false)}
          />
        )}
        <main id="main" tabIndex={-1} className="relative min-w-0 flex-1 overflow-auto focus:outline-none">
          {children}
        </main>
      </div>

      <CommandPalette
        open={searchOpen}
        onOpenChange={setSearchOpen}
        groups={groups}
        canSearchEmployees={canSearchEmployees}
      />
    </div>
  )
}

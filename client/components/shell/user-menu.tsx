"use client"

import { ChevronDownIcon, KeyRoundIcon, LogOutIcon, UserRoundIcon } from "lucide-react"
import Link from "next/link"
import { useTranslations } from "next-intl"
import { use } from "react"

import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

import { signOut } from "./sign-out-button"

/** Who is signed in, as the top bar shows it. Null name: an account with no employee record. */
export type ShellUser = { name: string | null; employeeCode: string | null; employeeId: string | null }

export function UserMenu({ userPromise }: { userPromise: Promise<ShellUser> }) {
  const t = useTranslations("shell.user")
  const user = use(userPromise)
  // No name with an employee record means the lookup failed; don't call that a system account.
  const name = user.name ?? (user.employeeId ? t("signedIn") : t("systemAccount"))

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="h-5 max-w-[240px] gap-1 px-1" aria-label={t("menuLabel", { name })}>
          <span
            aria-hidden="true"
            className="grid size-3.5 shrink-0 place-items-center rounded-full bg-brand-soft text-12 font-semibold text-brand"
          >
            {initials(name)}
          </span>
          <span className="hidden truncate text-14 sm:inline">{name}</span>
          <ChevronDownIcon aria-hidden="true" className="text-ink-muted" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-30">
        <DropdownMenuLabel className="flex flex-col">
          <span className="truncate text-14 font-medium text-ink">{name}</span>
          {user.employeeCode && <span className="font-mono text-12 text-ink-muted">{user.employeeCode}</span>}
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        {user.employeeId && (
          <DropdownMenuItem asChild>
            <Link href={`/employees/${user.employeeId}`}>
              <UserRoundIcon aria-hidden="true" />
              {t("profile")}
            </Link>
          </DropdownMenuItem>
        )}
        <DropdownMenuItem asChild>
          <Link href="/change-password">
            <KeyRoundIcon aria-hidden="true" />
            {t("changePassword")}
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => void signOut()}>
          <LogOutIcon aria-hidden="true" />
          {t("signOut")}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

export function UserMenuFallback() {
  return <span className="size-3.5 animate-pulse rounded-full bg-canvas" aria-hidden="true" />
}

function initials(name: string): string {
  const parts = name.trim().split(/\s+/)
  return ((parts[0]?.[0] ?? "") + (parts.length > 1 ? (parts.at(-1)?.[0] ?? "") : "")).toUpperCase()
}

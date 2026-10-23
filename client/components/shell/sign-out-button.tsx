"use client"

import { useState, type ComponentProps } from "react"

import { Button } from "@/components/ui/button"
import { auth } from "@/lib/api/client"

/** Ends the session, then reloads onto the login page so no cached data outlives it. */
export async function signOut() {
  try {
    await auth("logout")
  } catch {
    // The Route Handler clears the cookies even when the API is down; if it couldn't be reached at all,
    // the login page will say so.
  }
  // A full page load, not router.push: nothing cached for this user may survive the sign-out.
  // eslint-disable-next-line @next/next/no-location-assign-relative-destination
  window.location.assign("/login?reason=signed-out")
}

export function SignOutButton(props: Omit<ComponentProps<typeof Button>, "onClick">) {
  const [pending, setPending] = useState(false)
  return (
    <Button
      type="button"
      {...props}
      disabled={pending || props.disabled}
      onClick={() => {
        setPending(true)
        void signOut()
      }}
    />
  )
}

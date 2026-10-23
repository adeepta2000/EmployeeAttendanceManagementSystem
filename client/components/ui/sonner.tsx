"use client"
// tokens: 8px spacing, spec type scale (scripts/shadcn-8px.mjs)

import { Toaster as Sonner, type ToasterProps } from "sonner"
import { CircleCheckIcon, InfoIcon, TriangleAlertIcon, OctagonXIcon, Loader2Icon } from "lucide-react"

// The app has a single light theme (§4.2), so the toaster doesn't follow a theme provider.
const Toaster = ({ ...props }: ToasterProps) => {
  return (
    <Sonner
      theme="light"
      className="toaster group"
      icons={{
        success: (
          <CircleCheckIcon className="size-2" />
        ),
        info: (
          <InfoIcon className="size-2" />
        ),
        warning: (
          <TriangleAlertIcon className="size-2" />
        ),
        error: (
          <OctagonXIcon className="size-2" />
        ),
        loading: (
          <Loader2Icon className="size-2 animate-spin" />
        ),
      }}
      style={
        {
          "--normal-bg": "var(--popover)",
          "--normal-text": "var(--popover-foreground)",
          "--normal-border": "var(--border)",
          "--border-radius": "var(--radius)",
        } as React.CSSProperties
      }
      toastOptions={{
        classNames: {
          toast: "cn-toast",
        },
      }}
      {...props}
    />
  )
}

export { Toaster }

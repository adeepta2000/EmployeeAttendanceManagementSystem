"use client"

import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { useState, type ReactNode } from "react"

import { Toaster } from "@/components/ui/sonner"
import { TooltipProvider } from "@/components/ui/tooltip"
import { ApiError } from "@/lib/api/client"

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        // Retry only what a retry can fix: the network or the server, never a 4xx.
        retry: (failureCount, error) =>
          failureCount < 2 && error instanceof ApiError && (error.isNetworkError || error.status >= 500),
      },
      mutations: { retry: false },
    },
  })
}

export function Providers({ children }: { children: ReactNode }) {
  // One client per browser session; useState keeps it stable across renders.
  const [queryClient] = useState(createQueryClient)

  return (
    <QueryClientProvider client={queryClient}>
      <TooltipProvider delayDuration={300}>
        {children}
        <Toaster position="bottom-right" />
      </TooltipProvider>
    </QueryClientProvider>
  )
}

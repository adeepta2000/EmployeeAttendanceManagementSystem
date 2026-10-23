import type { Metadata, Viewport } from "next"
import { Inter, JetBrains_Mono } from "next/font/google"
import { connection } from "next/server"
import { NextIntlClientProvider } from "next-intl"
import { getLocale, getTranslations } from "next-intl/server"

import { Providers } from "@/components/providers"

import "./globals.css"

// Inter for the interface; JetBrains Mono for times, durations and employee codes (§4.2). Both are
// self-hosted by next/font, so the CSP needs no third-party font origin.
const inter = Inter({ subsets: ["latin"], variable: "--font-inter", display: "swap" })
const jetBrainsMono = JetBrains_Mono({ subsets: ["latin"], variable: "--font-jetbrains-mono", display: "swap" })

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("app")
  return {
    title: { default: t("name"), template: `%s · ${t("name")}` },
    description: t("description"),
    // An internal tool: keep it out of search engines.
    robots: { index: false, follow: false },
  }
}

export const viewport: Viewport = { themeColor: "#1b4d8f" }

export default async function RootLayout({ children }: LayoutProps<"/">) {
  // Every page renders per request: the CSP nonce from proxy.ts only reaches dynamically rendered HTML.
  await connection()
  const locale = await getLocale()

  return (
    <html lang={locale} className={`${inter.variable} ${jetBrainsMono.variable}`}>
      <body>
        <NextIntlClientProvider>
          <Providers>{children}</Providers>
        </NextIntlClientProvider>
      </body>
    </html>
  )
}

import type { NextConfig } from "next"
import createNextIntlPlugin from "next-intl/plugin"

const withNextIntl = createNextIntlPlugin("./i18n/request.ts")

const nextConfig: NextConfig = {
  // Cache Components (and with it Partial Prerendering) stays off: every screen depends on the signed-in
  // user, and the strict CSP in proxy.ts needs a per-request nonce, which Next.js documents as
  // incompatible with a prerendered static shell.
  poweredByHeader: false,

  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },

  // Security headers for every response, API routes included (§2.6). The CSP is per request, in proxy.ts.
  async headers() {
    const headers = [
      { key: "X-Content-Type-Options", value: "nosniff" },
      { key: "X-Frame-Options", value: "DENY" },
      { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
      // Geolocation stays available to this origin for the punch screen (FR-3.2).
      { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(self), payment=()" },
    ]
    if (process.env.NODE_ENV === "production") {
      headers.push({ key: "Strict-Transport-Security", value: "max-age=63072000; includeSubDomains" })
    }
    return [{ source: "/:path*", headers }]
  },
}

export default withNextIntl(nextConfig)

# Nordwin Attendance: web client

The Next.js 16 (App Router) front end of Nordwin Attendance. It is also the backend-for-frontend (BFF):
the browser never talks to the .NET API directly and never sees a token.

## Run it

```bash
cp .env.example .env.local   # API_BASE_URL points at the API (default http://localhost:5216)
npm install
npm run dev                  # http://localhost:3000
```

The API must be running (`dotnet run --project src/Attendance.Api` from the repository root).

| Script | Does |
|---|---|
| `npm run dev` | Development server (Turbopack) |
| `npm run build` / `npm start` | Production build and server |
| `npm run typecheck` | Route types plus `tsc --noEmit` |
| `npm run lint` | ESLint |
| `npm run ui:add -- <component>` | Adds a shadcn/ui component and converts it to the design tokens |

## How authentication works

```
Browser ──(httpOnly cookies, same origin)──▶ Next.js Route Handlers / proxy.ts ──(Bearer token)──▶ .NET API
```

- **Sign-in** (`POST /api/auth/login`): the Route Handler calls the API, receives the access and refresh
  tokens and stores them in `httpOnly`, `Secure`, `SameSite=Lax` cookies. The browser gets back only
  `{ mustChangePassword }`.
- **Data calls** go to `/api/backend/<api path>`. The handler checks the route is on an allowlist
  (`lib/bff/backend-routes.ts`), validates the query and body with the same Zod schema the form used, adds
  the access token on the server and relays the answer.
- **Refresh**: an expiring access token is renewed before the call, and a 401 triggers one renewal and one
  retry. `proxy.ts` does the same before rendering a page. Refreshes are single-flighted, because the API
  treats a refresh token presented twice as stolen and revokes the whole session.
- **CSRF**: state-changing calls must come from this origin and echo the `nw_csrf` cookie in an
  `X-CSRF-Token` header (double submit), on top of `SameSite` cookies.
- **Headers**: a per-request nonce CSP (`proxy.ts`), plus `X-Frame-Options`, `X-Content-Type-Options`,
  `Referrer-Policy`, `Permissions-Policy` and, in production, HSTS (`next.config.ts`).

Permissions in the token only decide what the UI shows. The API authorizes and scopes every request
itself.

## Design tokens

All tokens are CSS custom properties in `app/globals.css`, exposed as Tailwind utilities:

- **Palette**: `surface`, `canvas`, `ink`, `ink-muted`, `rule`, `brand`, `brand-soft`.
- **Status colours** (`present`, `absent`, `late`, `leave`, `weekly-off`, `holiday`, `overtime`): for
  attendance data only, always with their letter code (`components/status-pill.tsx`).
- **Type scale**: `text-12` to `text-30`, in Inter. Times, durations and codes use `font-mono`
  (JetBrains Mono).
- **Spacing**: the spacing unit is **8px** (`p-1` = 8px). Layout sizes: `h-topbar`, `w-sidebar`, `h-row`
  (40px), `h-row-compact` (32px), `max-w-form` (720px).

## Layout

| Path | Contents |
|---|---|
| `app/(auth)` | Sign in, forgot password, reset password, change password |
| `app/(app)` | The application shell and its screens |
| `app/api/auth/*`, `app/api/backend/[...path]` | The BFF Route Handlers |
| `proxy.ts` | Route protection, token renewal on navigation, CSP nonce, CSRF cookie |
| `lib/bff` | Server-only BFF code: cookies, session, refresh, upstream calls |
| `lib/schemas` | Zod schemas shared by forms and Route Handlers |
| `lib/api` | Browser-side API client and TanStack Query hooks |
| `messages/en.json` | Every UI string (next-intl), ready for a Bangla table |

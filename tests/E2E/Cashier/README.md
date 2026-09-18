# Cashier (Kasa) E2E suite

Real-browser (Chromium) end-to-end tests against the actual production Host
binary (`ALKAROS.Host.dll serve ...`) serving a web root assembled the same
way `deploy/docker/Dockerfile`'s `frontend-build` stage assembles `/srv/app`
— the built PosTerminal SPA (`Cashier.tsx` at `/`, `CustomerDisplay.tsx` at
`/display`, the screensaver settings route at `/settings/screensaver`) with
the vanilla `src/Clients/Cashier` shell copied under `/cashier` alongside it
— with a freshly migrated and seeded Postgres database. Not a mock, not a
stub.

V1-CUI-011: before this suite, `tests/E2E/` had a real Playwright suite only
for `WaiterPwa` (V1-WTR-026/027) — nothing for Kasa (Cashier/PosTerminal/
CustomerDisplay). WaiterPwa's own suite found two real, shipped bugs no
xUnit/HTTP test could catch (see its own README); this suite exists because
the same class of risk (a click that silently does nothing, a route that
never renders) can just as easily hide in Kasa and nothing was exercising it
in a real browser.

## Prerequisites

- A reachable Postgres instance (the same `alkaros-test-pg` container every
  other test project in this repo uses is fine — this suite only ever
  touches its own `alkaros_e2e_cashier` database, dropped and recreated on
  every run).
- `ALKAROS.Host` already built in `Debug` configuration
  (`dotnet build ALKAROS.slnx -c Debug` from the repo root) — this suite runs
  the compiled `src/Host/bin/Debug/net8.0/ALKAROS.Host.dll` directly.
- **PosTerminal already built** (`corepack pnpm build` from
  `src/Clients/PosTerminal`) — `global-setup.js` serves its `dist/` folder
  directly and refuses to start with a clear error if it does not exist. It
  is not rebuilt automatically (mirrors production: the Docker image builds
  it once, ahead of time, not per request).
- Node.js. Playwright is pinned to `1.63.0` — 1.48 hangs silently under
  Node.js 24 (same finding `tests/E2E/WaiterPwa` already made).

## Setup (once, or whenever the Playwright browser cache is missing)

```bash
cd tests/E2E/Cashier
npm install
npx playwright install chromium
```

## Running

```bash
cd src/Clients/PosTerminal && corepack pnpm build && cd -
ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres npx playwright test
```

A single `playwright test` invocation runs `global-setup.js` exactly once
(copies the vanilla Cashier shell into the built PosTerminal `dist/cashier`,
creates + migrates + seeds `alkaros_e2e_cashier`, starts the Host on
`http://127.0.0.1:5098`), runs every spec file in numeric-prefix order
against that one shared environment, then tears both down.

Set `E2E_HOST_LOG=1` to stream the real Host process's own console output
live.

## What's seeded (`lib/seed.js`)

One real cashier account (`e2e.kasiyer` / a real PBKDF2-hashed password —
the actual login screen is exercised, not bypassed with a cookie) holding
every permission in the catalog (this suite is about exercising real
screens, not re-testing the authorization model — that's
`tests/Modules/Identity/Authorization`'s own job, so this account also
covers the screensaver settings route's `catalog.manage` gate); the fixed
`KASA-1` table (`00000000-0000-0000-0000-000000000001`) the vanilla
`src/Clients/Cashier` client hardcodes as its own over-the-counter sale
target; two catalog products, one with a low on-hand quantity (3, to
exercise the remaining-count badge's `.is-low` styling) and one with a
normal one (500), both mapped to stock.

## Scope (V1-CUI-011's own In scope / Out of scope)

Covers the four scenarios the task requires:

1. `01-login-and-catalog.spec.js` — cashier login (PosTerminal's real
   `Cashier.tsx` form) → Kasa screen opens, catalog loads.
2. `02-stock-badge-and-kitchen-dispatch.spec.js` — against the **vanilla**
   `/cashier` shell (a separate, independently-shipped Kasa surface, see
   `docs/engineering/kasa-faz0-redesign-requirements.md` §2 for why two
   surfaces exist): the remaining-count badge renders correctly (`Kalan 3`
   / `is-low` vs `Kalan 500`), and dispatching to the kitchen succeeds
   end to end through the real `table-draft` → `submit-draft` →
   `send-to-cashier` sequence.
3. `03-screensaver.spec.js` — a manager uploads an idle-screen image
   (`/settings/screensaver`), a second browser context (a real separate
   device/origin in production) pairs as the customer display and shows it
   once idle; removing it restores the default branded card.
4. `04-complimentary-line.spec.js` — after a comp (applied via a real,
   authenticated HTTP call — the comp action itself has no button in
   PosTerminal's Cashier.tsx, confirmed by grep: only WaiterPwa's UI calls
   `.../comp`), the Kasa bill view still shows the item's real price and a
   separate aggregate "İndirim" line (regression coverage for V1-RMD-228),
   not a zero/invisible line.

Payment/Token terminal scenarios are explicitly out of scope — no real
device/contract exists yet (`V0-HUG-001` is `Blocked`); this suite only
covers Kasa features that are live today.

## Layout

- `global-setup.js` — verifies `PosTerminal/dist` exists, copies the
  vanilla Cashier shell into `dist/cashier` (same step
  `deploy/docker/Dockerfile`'s `frontend-build` stage does), creates the
  database, applies every `*.up.sql` in the verified manifest order
  (`lib/migrate.js`), seeds it (`lib/seed.js`), spawns the real Host and
  waits for `/health/ready`. Returns an in-process teardown function (stops
  the Host, drops the database) — no separate `global-teardown.js` file.
- `lib/passwordHash.js` — mirrors `PasswordHasher.cs`'s PBKDF2 encoding
  exactly, so a seeded password verifies against the real login endpoint.
- `lib/testHelpers.js` — `loginCashier()`/`startOrder()`, `readSeed()`
  (reads the `.e2e-state.json` `global-setup.js` writes for the specs to
  consume).
- `specs/*.spec.js` — numbered so Playwright's default run order matches
  the narrative dependency between them (the same seeded terminal session
  carries across all four).

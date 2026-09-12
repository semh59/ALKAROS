# WaiterPwa E2E suite

Real-browser (Chromium) end-to-end tests against the actual production Host
binary (`ALKAROS.Host.dll serve ...`) serving WaiterPwa's own `wwwroot`, with
a freshly migrated and seeded Postgres database — not a mock, not a stub.

This is what found two real, currently-shipped bugs on 2026-09-12 that no
existing xUnit/HTTP test could have caught (they never execute this actual
browser script): a login-flow trap double-push that left the whole app
permanently unclickable after a fresh login, and a stray `headers,` reference
that threw before `submit-draft` ever fired, silently breaking every single
"Gönder" (send to kitchen) click. Both fixed in `waiter-app.js` — see its own
comments at the fix sites for detail.

## Prerequisites

- A reachable Postgres instance (the same `alkaros-test-pg` Docker container
  every other test project in this repo uses is fine — nothing here is
  destructive to any *other* database, it only ever touches its own
  `alkaros_e2e_waiterpwa` database, dropped and recreated on every run).
- `ALKAROS.Host` already built in `Debug` configuration
  (`dotnet build ALKAROS.slnx -c Debug` from the repo root) — this suite runs
  the compiled `src/Host/bin/Debug/net8.0/ALKAROS.Host.dll` directly, it does
  not build it itself.
- Node.js. **Playwright 1.48 hangs silently (zero CPU, no error) under
  Node.js 24** — this project pins `@playwright/test` to `1.63.0`, which
  does not have that problem. If a future bump reintroduces a hang with no
  console output at all, suspect this Node/Playwright interaction first,
  not the test code.

## Setup (once, or whenever the Playwright browser cache is missing)

```bash
cd tests/E2E/WaiterPwa
npm install
npx playwright install chromium
```

## Running

```bash
ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres npx playwright test
```

(`ALKAROS_TEST_PG_HOST`/`_PORT`/`_USER`/`_PASSWORD` follow the same
convention as every other test project's `PgTestDatabase` — see
`tests/BuildingBlocks/TestHelpers/Fixtures/PgTestDatabase.cs`. Defaults to
`localhost:5432`/`postgres` when unset.)

A single `playwright test` invocation runs `global-setup.js` exactly once
(creates + migrates + seeds `alkaros_e2e_waiterpwa`, starts the Host on
`http://127.0.0.1:5099`), runs every spec file in numeric-prefix order
against that one shared environment, then tears both down. Specs `02` and
`03` deliberately depend on state the earlier specs left behind (the same
seeded table, in the order those tests put it in) — running only a later
spec file in isolation will fail for that reason; run the whole suite, or at
least from `01` through whichever spec you need.

Set `E2E_HOST_LOG=1` to stream the real Host process's own console output
(ASP.NET's request log, and any unhandled-exception stack trace) live —
this is how the load test's real Postgres deadlock (see `specs/05-*`'s own
comment) was actually root-caused, rather than guessed at from just the
HTTP status code.

## `05-load-and-timing.spec.js`

Two scenarios beyond the functional specs above:
- A single, dedicated 4-top (`E2E-TIMING`, its own 4 seats) ordered through
  the real options-sheet UI end to end, timed from opening the table to the
  kitchen ticket confirming — a floor, not a claim about how fast an actual
  person could do this (it is scripted clicking, with zero human decision
  time).
- 6 concurrent virtual waiters (`lib/seed.js`'s `LOAD-1..6` tables), each
  logging in, ordering and sending independently at the same time via
  `Promise.all` over separate browser contexts. This is what surfaced a
  real Postgres deadlock (`40P01`) in stock consumption under contention on
  a shared popular product — fixed in `SubmitOrderHandler.HandleAsync` with
  a bounded, backed-off retry (see that method's own comment). Run this
  spec a handful of times after touching anything in the stock-consumption
  path; a single green run is not strong evidence either way for a race
  this narrow a window.

`npx` on Windows Git Bash occasionally launches a broken Node process here
(observed hanging indefinitely, no output, on this same Node 24 install) —
if `npx playwright test` hangs with zero CPU usage and no output at all,
invoke the CLI directly instead:
`node node_modules/@playwright/test/cli.js test`.

## What's seeded (`lib/seed.js`)

One real waiter account (`e2e.garson` / a real PBKDF2-hashed password — the
actual login screen is exercised, not bypassed with a cookie) holding every
permission in the catalog (this suite is about exercising every waiter
action, not re-testing the authorization model — that's
`tests/Modules/Identity/Authorization`'s own job); a colleague account for
the transfer-server target; two tables (`E2E-1` with two floor-plan seats —
note the seat picker needs a full `zone_floor_plans` + `table_layouts` row,
not just a bare `table_seats` row, or it silently never renders; `E2E-2`
seatless, kept Available as the table-transfer target); two catalog
products, one plain and one carrying a modifier group (only a
modifier-bearing product opens the full options sheet with seat/course
pickers — a plain product quick-adds instantly with no picker at all), both
mapped to stock (submitting an order consumes stock and refuses the whole
submission for an unmapped product).

## Layout

- `global-setup.js` — creates the database, applies every `*.up.sql` across
  `database/migrations` in the same numeric order the verified manifest
  enforces (`lib/migrate.js`), seeds it (`lib/seed.js`), then spawns the real
  Host and waits for `/health/ready`. Returns an in-process teardown
  function (stops the Host, drops the database) — no separate
  `global-teardown.js` file.
- `lib/passwordHash.js` — mirrors `PasswordHasher.cs`'s PBKDF2 encoding
  exactly, so a seeded password verifies against the real login endpoint.
- `lib/testHelpers.js` — `login()`/`openSeedTable()`, `readSeed()` (reads the
  `.e2e-state.json` `global-setup.js` writes for the specs to consume).
- `specs/*.spec.js` — numbered so Playwright's default run order matches the
  narrative dependency between them (see "Running" above).

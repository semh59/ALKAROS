# V1-RMD-017 validation

- Candidate commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`.
- Repository root: `D:\PROJECT\ALKAROS`; active task `V1-RMD-017`; assignee `/root`.
- Dependencies `V1-RMD-013` and `V1-RMD-016`: `Done`.
- Owned surface is limited to `src/Clients/PosTerminal/src/features/tables/**` and this evidence directory.
- Component tests: `pnpm --dir src/Clients/PosTerminal test` exit `0`; 6 test files and 38 tests passed.
- Typecheck: `pnpm --dir src/Clients/PosTerminal typecheck` exit `0`.
- Production build: `pnpm --dir src/Clients/PosTerminal build` exit `0`; Vite `8.2.2` generated the bundle.
- Coverage includes map/list view, zone/status/search filters, authoritative row version and order/bill context,
  manager zone/table validation and creation, cashier transition/reservation/transfer/merge forms, conflict context
  preservation, offline/stale/unauthorized/error/loading/empty states, focus-safe named dialogs, and axe critical/
  serious checks.
- Real client contract tests cover terminal-bound versioned reads/creates, status/transfer/merge request bodies, and
  server `409` error envelopes. The client uses `credentials: same-origin`, an 8-second timeout, and no fabricated
  success response.
- Manual scenario for the integrated shell: manager creates `Salon` and `S-09`; cashier opens it, deliberately submits
  a stale row version, sees `409`, and returns to the authoritative card with order context preserved. Composition and
  browser execution are owned by `V1-RMD-020`; this task supplies the complete UI and client contract for that run.
- File SHA-256: `TableWorkspace.tsx=318047BAE0BEF039EF6C38B779C474187B7452635F2294078ACED92F20454A96`,
  `TableWorkspace.test.tsx=68A65020D2B96DC49D299F69EBFB9812F5C0B9F0B4C8C5A5897BFB1BF4855DB4`,
  `tableApi.ts=C68A1C6D90A447DC681296B765475F51B8A8DDBADDDE674632EA625898800E33`,
  `tableApi.test.ts=C64442CC8611DE9BCC11341C3DD97CA0864177F52610A7D86807ED2E836AC873`,
  `models.ts=1183CE37527CFD55B525A8FE0BCE66F1D004D1CCC09C4D5520A617C4A6BEB18B`,
  `tables.css=24F5C94645DFDF08474B3D13E05242275556AF024F37B2CDF2087239221C54DF`,
  `index.ts=0C14D5BBC30ED64A9F98790A7BBB5DB1F723C1A6F7E2BD41072F754048103EB5`.

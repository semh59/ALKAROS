# V1-RMD-020 validation

- Candidate commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Task: `V1-RMD-020`
- Assignee: `/root`
- Date: `2026-08-27`

## Composition and build checks

| Command | Exit | Result |
| --- | ---: | --- |
| `dotnet build src/Host/ALKAROS.Host.csproj --configuration Release` (SDK `10.0.302`, disposable container) | 0 | Host and all referenced modules built with 0 warnings / 0 errors |
| `dotnet test tests/Host/Experience/Composition/ALKAROS.Host.Experience.Composition.Tests.csproj --configuration Release` | 0 | Host exposes table-management, catalog-management and kitchen-operations route groups |
| `pnpm --dir src/Clients/PosTerminal test` | 0 | 10 files, 62 tests passed |
| `pnpm --dir src/Clients/PosTerminal typecheck` | 0 | TypeScript passed |
| `pnpm --dir src/Clients/PosTerminal build` | 0 | 67 modules transformed; production bundle emitted |

## Real PostgreSQL/HTTPS HTTP transcript

The disposable PostgreSQL 18 container used the digest-pinned
`postgres:18-alpine@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2` image, all 37 manifest
migrations, and a dedicated `alkaros_browser` database. A disposable Kestrel Host served the built bundle at
`https://127.0.0.1:58299` with a local certificate.

- Authenticated cashier + manager session read returned `200` and capabilities `catalog.manage`, `pos.cashier.mutate`.
- Manager catalog product list returned `200` with the seeded Espresso product; cashier-only catalog access returned `401`.
- Terminal-bound table list returned `200`; table creation returned `201` and created `S-09`.
- Product creation returned `201` and created `LAT-01`; cashier order creation, item add and submit returned `200`/`201`
  with authoritative revisions and total `121.00 TRY`.
- The table-order bridge now calls `POST /api/v1/terminals/{terminalId}/orders/table` with the selected table id and row
  version; the backend transaction test proves the order and table pointers are committed together before the cashier
  shell returns to the order workspace.
- Cashier submit now resolves a transaction-scoped `IOrderSubmissionDispatcher`; with `ALKAROS_KITCHEN_STATION_ID=MainKitchen`,
  the submitted order creates one `Queued` ticket and its active item graph. Missing station configuration throws before
  the request is acknowledged, so the endpoint has no unscoped success fallback.
- Kitchen ticket list returned `200` with station scope; customer-display-only access returned `403`.
- Missing terminal session returned `401`.
- Root bundle returned `200` with CSP, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` and
  `Cache-Control: no-cache`.

No customer, personnel, token, secret or internal-note fields were present in the catalog/table/kitchen DTO responses
used by this transcript.

## Browser result and blocker

The in-app Browser could not render the Host because its certificate trust policy rejected the disposable self-signed
certificate with `net::ERR_CERT_AUTHORITY_INVALID`. The Browser documentation requires fail-closed handling for this
interstitial and does not permit bypassing it. Therefore the required browser E2E (login, two storage areas,
table/product/kitchen workflow, reconnect/restart, stale/conflict and revoke) is **not evidenced** here and this task
cannot be marked `Done`.

The local HTTP/API transcript is valid evidence of server behavior only; it is not a substitute for trusted HTTPS browser
evidence.

The table-to-kitchen product-boundary finding was remediated in V1-RMD-009: the legacy bodyless cashier endpoint
remains backward-compatible, while the explicit table-order endpoint requires a table id and row version. No fabricated
client-side association is used; the selected table is sent to the authoritative backend transaction.

## Known non-task blocker

The first SDK-container attempt for the repository-wide migration composition suite stopped at environment setup because
that image has no `psql` executable (`Win32Exception: process 'psql' not found`). The same suite was then rerun in a
disposable SDK `10.0.302` container with the PostgreSQL 18.6 client supplied only as an ephemeral test dependency; all
93 tests passed. No repository or production artifact was changed by that dependency injection.

- `dotnet test tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --configuration Release`
  - Exit `0`; SDK `10.0.302`, PostgreSQL 18.6 client/server, `ALKAROS_KITCHEN_STATION_ID=MainKitchen`; 93 passed,
    0 failed, 0 skipped.
- `dotnet test tests/Modules/Kitchen/TicketLifecycle/ALKAROS.Kitchen.TicketLifecycle.Tests.csproj --configuration Release`
  - Exit `0`; PostgreSQL 18 server; 12 passed, 0 failed, 0 skipped; submit→ticket, replay and rollback paths.
- `dotnet test ALKAROS.slnx -c Release`
  - Exit `0`; SDK `10.0.302`, PostgreSQL 18 disposable server/client, `ALKAROS_KITCHEN_STATION_ID=MainKitchen`;
    every .NET test project completed with 0 failed and 0 skipped failures.

## File hashes

| Path | SHA-256 |
| --- | --- |
| `src/Host/DualScreen/DualScreenApplication.cs` | `6FC95BCF604A907FCE56EC261029F3A725D4642FDACBAEBAA13D141A2D7BA0A1` |
| `src/Modules/Orders/SubmitOrder/SubmitOrderHandler.cs` | `A6764B897AC8E926ADF5667E433C723E63B40CEA2FC5C0E31DC41D6815C7B243` |
| `src/Modules/Orders/SubmitOrder/IOrderSubmissionDispatcher.cs` | `436E3F9EDF115491A072D4880F7FDC19B698C1D82EF5E427D0C302BB87681553` |
| `src/Modules/Orders/SubmitOrder/SubmitOrderExceptions.cs` | `74AEF0FC145F5C54116C8489945FC37FA8A5C1D7EFBB0546AD30C1EDF2DC20FF` |
| `src/Modules/Kitchen/TicketLifecycle/IKitchenTicketRepository.cs` | `2384A95B2ACF243A1F648669B4657C4286FC626CCFD2D19A686A553F41125BD1` |
| `src/Modules/Kitchen/TicketLifecycle/PostgresKitchenTicketRepository.cs` | `D13296532AAD9E1EC0D5FE8C3A2CF6AC11CAE47986C5344D5605315B011FB81E` |
| `src/Modules/Kitchen/TicketLifecycle/KitchenOrderSubmissionDispatcher.cs` | `22FCB15B5DC3B7CAFAFE0136A5873552F8B190F4980D64A3F1BDAC5895EF2FB7` |
| `tests/Modules/Kitchen/TicketLifecycle/KitchenTicketTests.cs` | `5D85DCD9BA1357CC694E15CE47C9E063D7C1E101DD7AD3785035B473C45DC0EF` |
| `src/Clients/PosTerminal/src/App.tsx` | `849D775852EF702EBDC787D91A1404EADA3A7CE32965F0406DA7DA08D6B4D26B` |
| `src/Clients/PosTerminal/src/api.ts` | `EA66626BF36FE4F80126632303EE39587A676D896D8BE2803FBA9F465BEEABF8` |
| `src/Clients/PosTerminal/src/contracts.ts` | `7BF8423496BF36FBF1BBE4DB77ECD27DF8D5355CADB2EDE7CDE4E3DD31C1C337` |
| `src/Clients/PosTerminal/src/styles.css` | `32422EC8893D66A733F4A75539E7EA721FE25AAD0E251AE0DF2CF0C07683C02D` |
| `tests/Host/Experience/Composition/ALKAROS.Host.Experience.Composition.Tests.csproj` | `D9623BCC70D74BE4E01549195472B4786080FBF2305A9022D2DAD2CE4B7B0229` |
| `tests/Host/Experience/Composition/ProductionExperienceCompositionTests.cs` | `57E50A98529154DA5F345F5F166D503E73A646E0F7E3E5D5F3E705ECA028BBAC` |

# V1-RMD-034 verification

## Result

`RMD032-F001` is closed. The production UI no longer contains a hard-coded `hot-line` station. An authenticated
terminal runtime-configuration endpoint returns the Host's `ALKAROS_KITCHEN_STATION_ID` value through a one-field
allowlist DTO, and the kitchen route obtains this configuration before issuing station-scoped requests.

## Automated checks

| Check | Exit | Result |
| --- | ---: | --- |
| `docker build --target ui-build --tag alkaros-rmd034-ui:20260828 .` | 0 | TypeScript typecheck and Vite production build passed; manifest list `sha256:23259b3b565b9732ebe7f40085e5e52e29a7cc1fa42991ab7057090362397368`. |
| `docker run --rm alkaros-rmd034-ui:20260828 pnpm test` | 0 | 13 files and 79 tests passed, including authoritative and missing-station client tests. |
| `docker build --target host-build --tag alkaros-rmd034-host-build:20260828 .` | 0 | Locked restore and Release Host publish passed; manifest list `sha256:81be7f9a71c829e205d50830ca8d0fee35ff650c9bb29a726e4696ef92fd4194`. |
| Isolated `DualScreenAuthorizationHttpTests.RuntimeConfigurationRequiresTheBoundTerminalAndReturnsOnlyTheKitchenStation` | 0 | 1/1 passed against PostgreSQL 18; unauthenticated and wrong-terminal requests returned 401, valid request returned only `kitchenStationId`, missing Host configuration returned redacted 500. |
| `docker compose up -d --build` | 0 | migrate/provision completed; postgres, Host and proxy became healthy. |

The local shell has no `dotnet` executable. The final Host contract test therefore ran inside the pinned SDK image,
on the Compose network, with PostgreSQL client installed only in the disposable runner and the database password
mounted as a read-only secret. `DOTNET_ROLL_FORWARD=Major` allowed the net8 testhost to execute on the SDK 10 runtime;
no test output was written into the repository.

## Real production scenario

1. The existing manager session opened `https://localhost:8443/kitchen` after the rebuilt stack started.
2. The UI heading resolved to `kitchen-main istasyonu` and showed the previously hidden queued ticket.
3. A second cashier order `POS-20260828-164942-2FD46F037C88` was created and submitted through the UI.
4. The kitchen UI showed its `kitchen-main` ticket, accepted it, moved the item to Preparing and Ready, then moved
   the parent ticket to Ready.
5. PostgreSQL confirmed:
   - ticket `KT-POS-20260828-164942-2FD46F037C88-kitchen-main`
   - station `kitchen-main`
   - ticket status `Ready`, ticket row version 5
   - item status `Ready`, item row version 5
6. Browser console warnings/errors for the exercised fixed route: zero.

## Evidence files

- `kitchen-fixed-dom.txt` and `kitchen-fixed-1440x900.png`: authoritative station and visible original ticket.
- `cashier-second-submit-dom.txt`: second real submitted order.
- `kitchen-lifecycle-final-dom.txt` and `kitchen-lifecycle-final-1440x900.png`: final Ready transition.

## Scope note

The earlier acceptance findings for bill split, floor-plan bootstrap, reservation typing, merge/unmerge, catalog
reflow, desktop design quality, complete live accessibility and mandatory external evidence remain open. This task did
not conceal or broaden into those independent remediations.

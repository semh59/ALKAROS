# V1-RMD-027 Verification

## Environment

- Date: 2026-08-28
- Repository root: `D:\PROJECT\ALKAROS`
- Task owner: `/root`
- .NET SDK: `10.0.302`
- PostgreSQL: `postgres@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2`
- Disposable instance: `alkaros-rmd027-pg-20260828`, loopback port `55437`
- Every PostgreSQL test class created and removed its own empty database and applied the ordered V1 migration manifest.

## Delivered behavior

- Versioned read, equal, item/quantity, amount, custom and clear route definitions exist under the terminal-bound billing
  split-design API.
- Missing cashier session returns `401`; authenticated users without `pos.cashier.mutate` receive `403` for mutation.
- Operational owners use canonical stable person or floor-plan seat IDs. Seat IDs are checked against the bill's
  authoritative table inside the save transaction.
- Replacement uses a PostgreSQL `Serializable` transaction, locks the bill and allocation set, verifies the expected
  bill and allocation versions, validates current item quantities and exact payable/tax totals, replaces all rows, and
  advances the bill row version.
- Paid, partially paid and cancelled bills reject design mutation. Responses state `DesignOnly`; no endpoint executes or
  claims a payment.
- DTOs expose the bill, bill item, allocation and concurrency data required by the cashier workflow without notes,
  tokens, session material or customer-account details.
- Production Host composition of this route group belongs to the downstream container integration task `V1-RMD-031`;
  this task publishes and tests the complete Host endpoint mapper without modifying that reserved composition surface.

## Review-driven correction

The code-review pass found that allocation rows alone were insufficient as the only first-write serialization token.
Every save and clear now increments the locked bill row version. A real two-writer PostgreSQL test proves that only one
caller can commit from the same bill/allocation version snapshot.

## Commands and results

```text
dotnet restore tests/Host/Experience/Billing/ALKAROS.Host.Experience.Billing.Tests.csproj --locked-mode
Exit code: 0

dotnet build ALKAROS.slnx --configuration Release --no-restore
Exit code: 0; warnings: 0; errors: 0

dotnet test tests/Modules/Billing/SplitDesign/ALKAROS.Billing.SplitDesign.Tests.csproj --configuration Release --no-restore
Exit code: 0; passed: 27; failed: 0

dotnet test tests/Host/Experience/Billing/ALKAROS.Host.Experience.Billing.Tests.csproj --configuration Release --no-restore
Exit code: 0; passed: 3; failed: 0

dotnet test tests/Modules/Billing/BillFoundation/ALKAROS.Billing.BillFoundation.Tests.csproj --configuration Release --no-restore
Exit code: 0; passed: 36; failed: 0

dotnet test tests/Host/Experience/Composition/ALKAROS.Host.Experience.Composition.Tests.csproj --configuration Release --no-restore
Exit code: 0; passed: 2; failed: 0

dotnet format <Billing, Host and Host Billing test projects> --no-restore --verify-no-changes --include <owned files>
Exit code: 0 for all three runs
```

## Covered acceptance cases

- Stable seat/person owner reference round-trip.
- Deterministic equal, item/quantity, amount and custom totals, including exact tax and kuruş remainder.
- Cumulative item quantity overflow rejection with unchanged persisted design.
- Exact allocation-set and bill-version conflicts with `409` and conflict metadata.
- Two simultaneous first writers: one commit and one recoverable conflict.
- Atomic replacement, restart readback and clear.
- Bill-table seat membership rejection.
- Paid bill fail-closed behavior.
- Route and PostgreSQL repository registration.

## Manual scenario

With the downstream production composition enabled, sign in as an authorized cashier, open a table-bound bill, split it
equally between a persistent seat and a person, replace it with full item/quantity assignments, then replace it with
exact amounts. Refresh or restart the Host and confirm the authoritative design reloads. Submit a stale copy and confirm
the draft remains client-side while the server returns `409`; reload versions, clear the design, and confirm no payment
success is shown.

## Migration note

This task adds no migration. It uses the existing billing allocation schema and the `V1-RMD-026` floor-plan seat schema;
the HTTP tests apply the full ordered migration set to a fresh PostgreSQL database.

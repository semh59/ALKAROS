# V1-RMD-015 validation evidence

- Candidate commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Executor: `/root`
- Date: 2026-08-26
- PostgreSQL image: `postgres:18-alpine@sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2`
- Database lifecycle: a disposable PostgreSQL 18 container on host port `55432` and unique per-fixture databases were
  used; the container was removed after validation.

## Commands and results

1. `dotnet build src/Host/ALKAROS.Host.csproj -c Release --nologo`
   - Exit code: `0`
   - Exact SDK: `.NET SDK 10.0.302`; `0` warnings, `0` errors.
2. `dotnet test tests/Host/Experience/KitchenOperations/ALKAROS.Host.Experience.KitchenOperations.Tests.csproj`
   `-c Release --nologo`
   - Exit code: `0`
   - `4` passed, `0` failed, `0` skipped against real Kestrel HTTP and disposable PostgreSQL 18.
   - Covers 401/403 session and capability boundaries, ticket/item lifecycle with authoritative row versions and stale
     `409`, minimized DTOs, Unknown delivery recovery, reason-required reprint approval without automatic execution,
     failed health/backup visibility, fail-closed backup command, printer/route reads, and sanitized audit reads.
3. `git diff --check`
   - Exit code: `0` for the owned implementation/test/evidence paths.

The first test attempt without a running PostgreSQL endpoint failed before database initialization with connection
refused. It was not counted as acceptance evidence; the pinned disposable PostgreSQL run above is the acceptance run.

## Manual Host scenario

After `V1-RMD-020` wires `AddKitchenOperationsExperience` and `MapKitchenOperationsApi` into the real Host, create a
terminal-bound cashier session whose role grants `pos.cashier.mutate`, `kitchen.reprint`, `kitchen.routing.manage`, and
`operations.backup`. Submit an order, create its station ticket, advance the item from Queued to Preparing to Ready,
and advance the ticket only after the readiness invariant is true. Seed or observe an Unknown physical delivery, open
the Unknown queue, approve reprint with a supervisor reason, and verify the API remains `ReprintApproved` until a
separate physical transport worker executes it. Open latest health and recent backups and verify Unhealthy/Failed states
remain visible. Submit a backup command without a deployment-specific verified payload provider and verify `503
BACKUP_NOT_CONFIGURED`; no synthetic backup is accepted.

## SHA-256

```text
25360478506B08E60471DE1CC47FDE18B9D89002CD9FC2E3880114AEF334B265  src/Host/Experience/KitchenOperations/KitchenOperationsContracts.cs
947E57BBFB30D90D024F1975B939DA4809974D4C5111BBC7A26748C982FF30F1  src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs
69ECCF3D43AB7A17F050FA5CB2FAED4EC4A17E2E1C137D32A61160D1D593BA84  src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs
8B2804399C77F4C91F9CE922AB83B970203F60295743E150D8BB5AE9AD386BE3  tests/Host/Experience/KitchenOperations/ALKAROS.Host.Experience.KitchenOperations.Tests.csproj
FF9C49B3ED73E25C7754F1ADB21A2162F6AB31DE48DDE78E793C3D3BD08C65E5  tests/Host/Experience/KitchenOperations/KitchenOperationsHttpTests.cs
21E630E661572647FF3125E1BB34E51CF3A81C0002C8D7D2853BD04E4EA5C69C  tests/Host/Experience/KitchenOperations/KitchenOperationsTestDatabase.cs
E5BF26F95F30C290EDB6204516BA1E7615FC6C1EF7F2596A21BF5FCFCF8B1D59  tests/Host/Experience/KitchenOperations/packages.lock.json
```

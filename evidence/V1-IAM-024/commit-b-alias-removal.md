# V1-IAM-024 — Commit B: drop the pos.cashier.mutate alias

Date: 2026-09-04
Scope: after commit A re-pointed every endpoint to a granular code, the
transitional `pos.cashier.mutate` permission has no remaining reader. This
commit removes it from the catalog, the seed path and the manifest.

## Changes

- `database/migrations/V1/V1-IAM-024/049-authorization-drop-mutate-alias.{up,down}.sql`
  - up: `DELETE FROM identity.permissions WHERE code = 'pos.cashier.mutate'`;
    `identity.role_permissions` rows for it drop via the migration-008
    `ON DELETE CASCADE` FK.
  - down: re-inserts the permission (same id `2a...0001` as migration 042) and
    re-grants it to cashier / supervisor / manager (not waiter).
- `database/MigrationComposition/order.json`: new `049` entry
  (`phase B`, `tables: ["permissions","role_permissions"]`), `phaseBRange.max`
  `048` -> `049`.
- `src/Host/Composition/Migrations/MigrationManifest.cs`: `PhaseBMax` `048` ->
  `049` (ceiling bump only; the const stays V1-FND-004-owned) plus its range
  comment.
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`: `049` added to the
  id list, count `47` -> `48`, `LastEntryTables` ->
  `["permissions","role_permissions"]`, the out-of-range case now uses `050`.
- `src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs`: removed
  `PosCashierMutateAlias` and `RolesWithMutateAlias`; class + `RoleGrants`
  summaries updated (no more "until V1-IAM-024" / alias sentences).
- `src/Host/Program.cs`: `ManagerPermissions` drops the `pos.cashier.mutate`
  row; the comment now states the granular §2-3 codes are granted to manager by
  migration 043 and are not repeated in provisioning.
- `tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabase.cs`:
  the fixture chain gains `049`; `ApplyDownSplitAsync` now runs `049` down then
  `043` down (strict descending order).
- `tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabaseTests.cs`:
  the cashier / supervisor / manager assertions flip to "no longer holds the
  alias"; the down test asserts `049` down restores it.
- `tests/Modules/Identity/Authorization/Catalog/ApplicationPermissionsTests.cs`:
  the alias literal replaces the removed const; the `RolesWithMutateAlias` test
  is deleted.
- `PermissionSplitMigrationTests.cs` untouched — it asserts migration 043's
  immutable up/down text, which still contains the historical alias JOIN.

## Verification (local Postgres 18)

- `dotnet build ALKAROS.slnx -c Release`: 0 Uyarı / 0 Hata.
- `dotnet test tests/Modules/Identity/Authorization` (Debug): 179/179. The
  `Catalog` DB-state tests exercise 005->008->042->043->049 and its reversal.
- `dotnet test` `Manifest.ManifestTests` (Debug): 16/16. `HostConstructability`
  4/4.
- Migration chain via `docker exec alkaros-test-pg psql`:
  - full `001..049` up on an empty DB: clean. Post-049 `pos.cashier.mutate`
    rows = 0; granular permission rows = 10; role grant counts waiter 3 /
    cashier 8 / supervisor 14 / manager 17.
  - `049` down: alias row back to 1, granted to cashier + supervisor + manager
    only. `049` re-up: alias row back to 0.
- `plan-audit validate` / `verify-manifest` / `validate-coverage`: 0 errors
  (`AUDIT_MANIFEST.json` + `AUDIT_REPORT.md` regenerated). `project-manifest`:
  VALID. `GATE-V1-EXIT` reseal deferred to a governance task.
- Not run locally (environmental): Kitchen HTTP + Experience Composition (WDAC
  `0x800711C7` on rebuilt module DLLs), `MigrationExecutionTests` (`psql` not on
  host PATH). Covered on the CI Postgres leg.

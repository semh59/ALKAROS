# V1-IAM-024 — Commit A: re-point endpoints to granular permission codes

Date: 2026-09-04
Scope: replace the transitional `pos.cashier.mutate` check on every Experience
endpoint with the granular code from docs/domain/authorization-model.md §2-3.
The `pos.cashier.mutate` alias grant (migration 042/043) is left in place; it is
now an unused grant removed by Commit B (migration 049).

## Endpoint -> permission map applied

| Surface | Endpoint(s) | Code |
| --- | --- | --- |
| Tables | /zones POST/PUT/DELETE, /tables POST, /tables/{id} PUT, /floor-plans/{zoneId} PUT | floorplan.manage |
| Tables | /tables/{id}/status | tables.status |
| Tables | /reservations, /reservations/{id}/claim, /cancel, /expire | tables.reserve |
| Tables | /transfers | tables.transfer |
| Tables | /merges, /merges/{groupId}/unmerge | tables.merge |
| Billing | split-design (all 6 routes) | bills.split |
| DualScreen | /orders, /orders/table, /orders/{id}/items add|patch|delete | orders.create |
| DualScreen | /orders/{id}/submit | orders.send |
| DualScreen | /pairings/approve, /display-sessions/revoke | session only (RequireCashierAsync) — device ops, no floor code fits, effective access unchanged |
| Kitchen | KitchenOperationsEndpoints.TicketMutationPermission (ticket + item transitions) | orders.send |
| Orders module | /table-draft (create/edit) | orders.create |
| Orders module | /{orderId}/submit | orders.send |

`TableManagementPrincipal` changed from `(Guid, bool CanMutate)` to
`(Guid, IReadOnlySet<string> Permissions)`; `TableContractMapper.AllowedCommands`
filters each command by its own permission instead of an all-or-nothing gate.
`OrderManagementEndpoints` gained a real `IAuthorizationService` check on the two
mutating routes (was session-only) plus its DI registrations.

## Verification (local Postgres 18, this repo)

- `dotnet build ALKAROS.slnx -c Release`: 0 Uyarı / 0 Hata.
- `dotnet test tests/Host/Experience/Tables` (Debug): 8/8. Denial test now asserts
  `denial_events.permission_code = 'floorplan.manage'`; mutate seed grants the 5
  granular table codes.
- `dotnet test tests/Host/Experience/Billing` (Release): 3/3. Split seed grants
  `bills.split`.
- `dotnet test tests/Host/Experience/Orders` (Debug): pass (store-level, no HTTP).
- `dotnet test tests/Modules/Identity/Authorization` (Debug): 180/180.
- `dotnet test` HostConstructability (Debug): 4/4 — DI graph still validates with
  the new `IAuthorizationService` registration in `AddOrderManagementExperience`.
- Migration chain: full 001..048 up applied clean to a scratch DB; 048 down then
  re-up clean. Commit A adds no migration.
- `plan-audit validate` / `verify-manifest` / `validate-coverage`: 0 errors.
- `project-manifest`: VALID (0 differences).
- Kitchen HTTP + Experience Composition tests: not run locally — WDAC
  (`FileLoadException 0x800711C7`) blocks the freshly-built module DLLs in this
  environment (G1). Kitchen change is a const value swap (`pos.cashier.mutate` ->
  `orders.send`) with the matching denial-assertion update; Composition is covered
  by HostConstructability above.

Alias still present by design in: `Program.cs` ManagerPermissions row,
`ApplicationPermissions.PosCashierMutateAlias` / `RolesWithMutateAlias`,
migration 042/043 text, `PermissionSplitMigrationTests`. All removed in Commit B.

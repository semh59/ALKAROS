# Authorization wave — remediation plan

Working document for the next session. Consolidates every defect, gap and
inconsistency found auditing the V1-IAM-016..024 differentiated-authorization
wave as landed on `master` (commit `bc8de14`, 2026-09-04). Not a governed task
file; the ordered plan below is the intended execution sequence.

## Current state on master

Done + merged (ff-only): V1-IAM-016/017 (model + permission split, migration
043), V1-IAM-018 (policies, 044), V1-IAM-019 (grant engine, 045), V1-IAM-021
(delegations, 046), V1-IAM-022 (offline authority, 047), V1-IAM-023 (behavioural
tightening, 048), V1-IAM-020 (manager decision surface). `PhaseBMax = 048`.
Migration chain 001..048 verified forward + full reverse. `dotnet build -c
Release` 0/0. `plan-audit validate` / `validate-coverage` / `verify-manifest` /
`project-manifest` / `markdownlint` all clean.

Not done: V1-IAM-024 (`Status: Blocked`). The authorization engine is built,
DI-registered and unit/integration-tested, but its **requester side is not
connected to any live HTTP path** (see section C).

## Audit scope

Rigorous single-pass analysis across ~15 angles (SQL / injection, migration
up/down/chain, DI validation, concurrency / idempotency, transactions, indexes,
PostgreSQL error codes, timezones, audit completeness, rate limiting, CSP,
frontend runtime, test meaningfulness, doc drift, governance) on the session's
139-file blast radius. It does NOT cover the other ~190 V1 tasks (sealed by
V1-GOV-070) and is not a substitute for a formal security review, load test,
second reviewer, or state-machine verification.

## Findings register

### A — Real defects

- **A1 [MED, dormant] Offline budget re-issue FK crash.**
  `PostgresOfflineAuthorityBudgetRepository.CreateAsync` runs
  `DELETE FROM identity.offline_authority_budgets WHERE session_id = @session`
  before the insert. `identity.offline_authority_replays.budget_id` has a plain
  FK to `offline_authority_budgets` with **no `ON DELETE CASCADE`** (unlike
  `_lines`), so if the prior session budget already has replay rows the DELETE
  throws `foreign_key_violation`. Dormant only because `IssueAsync` has no
  caller. Fix: new migration drops `uq_offline_authority_budgets_session`;
  `CreateAsync` stops deleting; `GetBySessionAsync` / `LoadAsync` gains
  `ORDER BY issued_at DESC LIMIT 1`.
- **A2 [LOW, history only] Commit `4cb77b4` CRLF churn** on
  `MigrationManifest.cs` / `ManifestTests.cs`; net-corrected by `ba5861a`. Not
  worth rewriting published history.

### B — Fixed this session

- **B1** Own-check guard was role-agnostic and covered `bills.discount`. Fixed
  in `bc8de14`: scoped to the `waiter` role, set is now
  `{bills.void, bills.comp}` (model §3 marks only void/comp "(own check)";
  cashier's is a plain grant).

### C — Structural gaps (engine built + tested, NOT wired; no plan task covers)

- **C1** No Experience endpoint calls `IAuthorizationGrantService.RequestAsync`
  — a caller lacking the outright permission gets 403, not a pending grant.
- **C2** Login never calls `IOfflineAuthorityBudgetService.IssueAsync` — devices
  get no offline budget.
- **C3** No reconnect endpoint calls `IOfflineGrantReconciler.ReconcileAsync`.
- **C4** `TableContractMapper.AllowedCommands` is still the coarse `canMutate`
  boolean, not "held permissions ∪ reachable grants" (model §6).
- **C5** Consequence of C1: `BehaviouralTighteningGate` and
  `DelegationEscalationResolver` never fire in production (only reachable via
  `RequestAsync`).

### D — Minor code issues (low severity, latent)

- **D1 [LOW]** `GrantRequest.Validate()` does not reject
  `RequesterUserId == Guid.Empty` (`OfflineAuthorizedAction.Validate()` does).
- **D2 [LOW]** `PostgresAuthorizationPolicyRepository.UpsertAsync` with a
  non-null `expectedRowVersion` for a non-existent scope silently INSERTs
  (`row_version = 1`) instead of raising
  `AuthorizationPolicyConcurrencyException` — "replace of a concurrently-deleted
  policy" becomes a create.
- **D3 [LOW]** `AuthorizationDecisionEndpointFilter.MapError` has no
  `ArgumentException` -> 400 branch (Catalog's filter does). No current code
  path produces one; latent only.
- **D4 [LOW-MED, audit gap]** `identity.authorization_delegations` has
  `revoked_at` but no `revoked_by_user_id`; `RevokeAsync(id, at)` and
  `AuthorizationDecisionStore.RevokeDelegationAsync` take no actor. A manager
  revoke via the decision surface loses who revoked — inconsistent with
  `behavioural_tightenings.cleared_by_user_id` and
  `authorization_grants.approver_user_id`; weakens model §8 "one audit row per
  action". Fix: new migration adds `revoked_by_user_id UUID`; thread the actor
  through repo, store and endpoint.
- **D5 [LOW, perf at scale]** `PostgresBehaviouralRateSource.CountGrantedSinceAsync`
  (filter `requester_user_id` + `permission_code` + `status = 'granted'` +
  `resolved_at >= since`) has no covering index —
  `ix_authorization_grants_auto_window` is partial to `policy_path = 'auto'`
  while this counts all granted. Sequential scan per behavioural gate check.
  Fix: partial index on `(requester_user_id, permission_code, resolved_at)
  WHERE status = 'granted'`.
- **D6 [LOW, test gap]** No test exercises the `/authorization` route glue in
  `workspace.tsx` (`AuthorizationDecisionsRoute` fetch / act wrapper); the
  workspace component and the API client are each unit-tested, the wiring is
  not.
- **D7 [VERY LOW, TOCTOU]** `OfflineGrantReconciler` — if a budget is deleted
  between `GetAsync` and `RecordAsync`, the replay insert hits FK `23503` (only
  `23505` is caught) -> unhandled `PostgresException` -> 500 instead of a
  graceful error.
- **D8 [VERY LOW]** `OfflineAuthorizedAction.Validate()` does not bound
  `OfflineAuthorizedAt`. Future-beyond-reconcile is caught by `RecordAsync`'s
  guard plus the DB CHECK; ancient stamps are denied as "expired" — no crash.

### E — Doc drift in `docs/domain/authorization-model.md` (V1-IAM-016-owned)

- **E1** §1 line 30 "auto-revoked" and §7 line 163 "V1-IAM-021 ... + auto-revoke
  job" — the implementation has no auto-revoke job; expiry is enforced by the
  query filter, `revoked_at` is early cancel.
- **E2** §1 line 31, §5 lines 125-136 "signed offline authority budget
  (JWT-style)", §8 line 177 "bounded by the signed budget ... session-scoped
  exp" — the implementation uses a server-held authority row keyed by an
  unguessable `budget_id` (recorded in the V1-IAM-022 task's "Tasarım sapması"
  note); there is no JWT.
- **E3** Model header `Status: Planned` while the V1-IAM-016 task file is
  `Status: Done` and 016-023 + 020 shipped.

### F — Governance

- **F1** `GATE-V1-EXIT` was closed by V1-GOV-070; this wave landed new V1 work
  after that (reopens the gate) but no commit recorded the reopen in
  `plan/GATES.md`. Needs the V1-GOV-066/068 pattern: `V1-GOV-071`
  (master-custody-reopen + remediation wave) + `V1-GOV-072` (audit reseal +
  gate closure).
- **F2** task-scope `enforce` cannot be satisfied for the Done-transition
  commits (Owned surface / Acceptance evidence filled in the same commit as
  `Status: Done`). The whole wave (017/018/019 included) does this; `enforce`
  does not run on push, and `plan-audit validate` is clean.
- **F3** `fix(v1-iam-019)` (`bc8de14`) touched files owned by V1-IAM-021 /
  V1-IAM-023 without custody notes. Follows the repo's `fix(...)` precedent.

### G — Environmental (not code, not fixable here)

- **G1** WDAC block on `ALKAROS.Identity.Authorization.Tests` Release DLL on
  this Windows host; Debug run is 180/180.
- **G2** ~40 `ALKAROS.Host.Tests` failures = `psql` not on the host PATH; CI
  installs a psql wrapper.
- **G3** Parallel `dotnet test ALKAROS.slnx` shows Postgres-contention flakes
  (5 in Identity.Authorization, 1 in Catalog); every affected project passes in
  isolation.

### P — Provisioning

- **P1** No bootstrap path creates a `waiter` user (`Program.cs
  provision-manager` only does manager). Decide whether in scope for the wave
  or a separate task.

### Cleared on deep re-check (NOT findings)

- DI graph soundness — `HostComposition.ComposeModules` builds with
  `ValidateOnBuild = true, ValidateScopes = true`; `HostConstructabilityTests`
  (10/10) validates `IAuthorizationGrantService`, `IOfflineGrantReconciler`,
  `IOfflineAuthorityBudgetService`, the resolvers and the gates are
  constructable.
- SQL injection — every value in every new Postgres repo is parameterized.
- Migration down symmetry — 044-048 clean; 043 down's choice to leave migration
  042's alias grants is intentional and documented.
- `timestamptz` scalar reads — only `MostRecentClearAsync` had the
  `InvalidCastException` class of bug (fixed this session with a reader); the
  other `ExecuteScalarAsync` calls read `count(*)` or a `Guid`.
- Rate limiting — absent on `/api/v1/management/authorization`, matching the
  existing `/api/v1/management/*` convention (the `terminal-write` limiter is
  for high-frequency cashier endpoints).
- CSP — the new endpoints are same-origin; no policy change needed.

## Execution plan (ordered)

Each phase leaves `master` green (build 0/0, `plan-audit validate` clean,
migration chain forward + full reverse clean) and is pushed before the next.

### Phase 0 — Reopen record (no code)

- **0.1** Create `V1-GOV-071` (master-custody-reopen + remediation-wave-25),
  following `V1-GOV-066` / `V1-GOV-068`. It enumerates the wave's custody
  transfers and registers the remediation tasks below. Add the "wave 25 reopen"
  row to `plan/GATES.md`.

### Phase 1 — Doc reconciliation (one commit, `docs(v1-iam-016)`)

- **1.1** E1 / E2 / E3 — amend `docs/domain/authorization-model.md`: §1, §5, §7,
  §8 to describe the shipped design (server-held budget row, no auto-revoke
  job); resolve the header `Status`. V1-IAM-016-owned edit.

### Phase 2 — Low-risk code hardening (one commit, `fix(v1-iam)`)

No migration, no wiring. Bundle:

- **2.1** D1 — `GrantRequest.Validate()` rejects `RequesterUserId ==
  Guid.Empty`; add a test.
- **2.2** D2 — split `PostgresAuthorizationPolicyRepository.UpsertAsync`: when
  `expectedRowVersion` is non-null, do an `UPDATE ... WHERE row_version =
  @expected RETURNING`; zero rows -> `AuthorizationPolicyConcurrencyException`.
  Only `INSERT` when `expectedRowVersion` is null. Add a test for
  "replace of a deleted policy".
- **2.3** D3 — add `ArgumentException` to
  `AuthorizationDecisionEndpointFilter`'s catch list and a `-> 400` branch in
  `MapError`.
- **2.4** D8 — `OfflineAuthorizedAction.Validate()` rejects `OfflineAuthorizedAt
  == default`.
- **2.5** D7 — `OfflineGrantReconciler.StoreAsync` also catches PostgreSQL
  `23503` and surfaces a typed `UnknownOfflineAuthorityBudgetException` (the
  budget vanished mid-reconcile).

### Phase 3 — Schema hardening (migration 049, one commit, `fix(v1-iam)`)

New migration `database/migrations/V1/V1-IAM-*/049-authz-wave-hardening.up/down.sql`
with `order.json`, `PhaseBMax` 048 to 049, and `ManifestTests` wiring. This
claims slot 049; V1-IAM-024's alias-removal migration becomes 050.

- **3.1** A1 — drop `uq_offline_authority_budgets_session`; change
  `PostgresOfflineAuthorityBudgetRepository.CreateAsync` to stop deleting the
  prior budget; `GetBySessionAsync` / `LoadAsync` add `ORDER BY issued_at DESC
  LIMIT 1`; add a "re-issue after reconciliation" DB test.
- **3.2** D4 — `ALTER TABLE identity.authorization_delegations ADD COLUMN
  revoked_by_user_id UUID`; `RevokeAsync(id, at, revokedByUserId)`;
  `AuthorizationDecisionStore.RevokeDelegationAsync(id, actorId)`; the endpoint
  passes `ActorId(http)`; DB test asserts the actor is stored.
- **3.3** D5 — `CREATE INDEX ix_authorization_grants_granted_rate ON
  identity.authorization_grants (requester_user_id, permission_code,
  resolved_at) WHERE status = 'granted'`; a migration-text test asserts it.

### Phase 4 — V1-IAM-024 endpoint re-pointing + alias removal

Unblock per the task's Blocker first (custody-transfer notes, regen
`AUDIT_MANIFEST`, `validate` + `verify-manifest` clean, set `Status: Planned`).
Then two commits, each leaving `master` secure:

- **4.1 Commit A — re-point.** Every mutation endpoint checks its granular code
  (mapping below); `AllowedCommands` filters per held permission; test seeds
  grant granular codes. The `pos.cashier.mutate` grant stays in place
  (harmless) so `master` is consistent + secure between the two commits.
- **4.2 Commit B — remove the alias.** Migration 050 (`DELETE FROM
  identity.permissions WHERE code = 'pos.cashier.mutate'`, FK
  `ON DELETE CASCADE` clears `role_permissions`; down re-creates + re-grants to
  cashier / supervisor / manager); remove `ApplicationPermissions.PosCashierMutateAlias`
  and `RolesWithMutateAlias`; remove the `Program.cs` `ManagerPermissions`
  line; update the remaining test seeds; bump `PermissionSplitDatabase` through
  migration 050 and flip its alias assertions. Migrations 042 / 043 and
  `PermissionSplitMigrationTests` keep the string as historical record (merged
  migrations are immutable).

Endpoint -> permission mapping (production-correct, locked 2026-09-04):

| Endpoint | Permission |
| --- | --- |
| Tables `/zones` POST/PUT/DELETE, `/floor-plans/{zoneId}` PUT, `/tables` POST, `/tables/{id}` PUT | `floorplan.manage` |
| Tables `/tables/{id}/status` | `tables.status` |
| Tables `/reservations` create / claim / cancel / expire | `tables.reserve` |
| Tables `/transfers` | `tables.transfer` |
| Tables `/merges`, `/merges/{id}/unmerge` | `tables.merge` |
| Billing split (all routes) | `bills.split` |
| DualScreen `/orders`, `/orders/table`, `/orders/{id}/items` add / patch / delete | `orders.create` |
| DualScreen `/orders/{id}/submit` | `orders.send` |
| Kitchen ticket + item `transition` | `orders.send` (within vocabulary, no new code) |
| Orders Experience module (`/table-draft`, `/submit`, ...) — currently session-only | ADD `orders.create` / `orders.send` checks (closes a real gap) |
| Customer-display `/pairings/approve`, `/display-sessions/revoke` | drop to `RequireCashierAsync` (session-only) — not a floor operation, no granular code fits, effective access unchanged |
| Cash | no endpoint exists in `src/Host/` — no-op |

`AllowedCommands` (Tables): replace `TableManagementPrincipal(Guid, bool
CanMutate)` with `(Guid, IReadOnlySet<string> Permissions)`;
`TableContractMapper.AllowedCommands` filters each command
(`Update` -> `floorplan.manage`; `Set*` -> `tables.status`;
`Reserve` / `ClaimReservation` / `CancelReservation` -> `tables.reserve`;
`Transfer` -> `tables.transfer`; `Merge` -> `tables.merge`). No grant union is
needed here — no table command is grant-reachable per model §3.

### Phase 5 — Requester-side wiring (new task `V1-IAM-025`)

Connects the engine (C1-C5). Register it in the Phase 0 `V1-GOV-071` cluster.

- **5.1** C1 — a mutation endpoint whose caller lacks the outright permission
  raises `IAuthorizationGrantService.RequestAsync` and returns a `pending`
  response (with the grant id + idempotency key) instead of 403; a re-submit
  with the same idempotency key after approval proceeds.
- **5.2** C4 — `AllowedCommands` becomes held ∪ reachable-grants so the client
  can render "Void (needs approval)". (Bills / cash actions on the client, not
  the Tables contract.)
- **5.3** C2 — login (`DualScreenApplication.Endpoints.cs` login handler) calls
  `IOfflineAuthorityBudgetService.IssueAsync(userId, roleCode, sessionId)` and
  returns the budget to the client.
- **5.4** C3 — a `POST /api/v1/terminals/{id}/offline-reconciliation` endpoint
  calls `IOfflineGrantReconciler.ReconcileAsync`.
- **5.5** D6 — add a `workspace.tsx` test that renders the `/authorization`
  route for a `reports.view` capability set and asserts the fetch wrapper wires
  the store callbacks.
- **5.6** C5 falls out of C1 — add integration tests proving the behavioural
  gate and the delegation resolver fire on a real `RequestAsync` HTTP path.

### Phase 6 — GATE-V1-EXIT reseal

- **6.1** Create `V1-GOV-072` (wave-25 master audit reseal + gate closure)
  following `V1-GOV-067` / `V1-GOV-069`: full build / test evidence, the
  "wave 25 reopen + reseal" row appended to `plan/GATES.md` (~line 36), gate
  closed. Requires Phases 1-5 complete and every gate green.

### Phase 7 — Provisioning (optional, decide scope)

- **7.1** P1 — decide whether `Program.cs provision-manager` (or a new
  `provision-role`) should also create a `waiter` (and `cashier` / `supervisor`)
  bootstrap user. If yes, small task; if the operator seeds staff another way,
  document that and close.

# V1 independent audit — pre-wave surface (the ~190 tasks)

Independent static / cross-layer review of the V1 surface **excluding** the
V1-IAM-016..024 authorization wave (that has its own register in
`authz-wave-remediation-plan.md`). Target: everything sealed by V1-GOV-070
(commit `3931930`) — all Modules, Host / Experience, the three clients,
migrations 001-042, BuildingBlocks, infra.

This is not a formal penetration test, load test, or line-by-line certification.
It is a structured pass for the defect classes the repo's own audit history
(`plan/GATES.md`, V1-GOV-026..036) repeatedly surfaced: unwired routes,
mock / fallback leftovers, missing authorization, non-atomic multi-writes,
silent data loss, client / server contract drift, regex / parser holes, XSS,
enum leaks, render loops, compile errors, missing indexes, TOCTOU.

Because the master branch is the product of ~25 audit + fix cycles, the surface
is already hardened; a complete independent audit of 190 tasks is a
multi-session programme. This document is **Round 1** — a down payment plus the
map for the rounds that follow.

## Round 1 — baseline verified

- `dotnet build ALKAROS.slnx -c Release`: 0 warnings / 0 errors.
- Full `dotnet test ALKAROS.slnx -c Debug`: every project passes in isolation.
  The only failures are environmental: `ALKAROS.Host.Tests` (~40, `psql` not on
  the host PATH; CI installs a wrapper), `Identity.Authorization` (5) and
  `Catalog.ProductCatalog` (1) flake under 40-way parallel Postgres contention.
- No `mock` / `stub` / `fake` / `TODO` / `FIXME` / `NotImplementedException` in
  production `src/` (`.cs` / `.ts` / `.tsx`, excluding `node_modules` / tests).
- No string-concatenated SQL values in any repository — every value is a bound
  parameter.
- Every Host endpoint file wires an authentication + authorization guard
  (`AddEndpointFilter` / `RequireCashier*` / `RequireMutation*` / a manager
  cookie filter).
- 47 / 47 migrations have a `.down.sql`; the full chain applies forward and
  reverses to empty (verified).
- `AuditSanitizer` structured path (`JsonNode` recursion) plus a length-capped
  regex fallback for malformed JSON — was hardened by V1-GOV-028 / 032.
- `WaiterOfflineQueueEngine` retains failed operations (no drop on error),
  breaks the replay loop on first failure to preserve FIFO — the V1-GOV-034
  "silent data loss on 4xx" fix is in place.
- Cashier client carries no payment / tender flow at all (`PaidAmount` /
  `IsFullyPaid` are read-only server-projected view models) — the historical
  "fake payment" path was removed, not stubbed.

## Round 1 — findings

### H1 [MED] `BillingSplitStore.CreateBillFromOrder` — broad `catch (Exception)`

`src/Host/Experience/Billing/BillingSplitStore.cs` ~line 51:

```csharp
try { await _bills.AddAsync(bill, cancellationToken); }
catch (Exception)                       // <-- catches everything
{
    // "recover gracefully" from a concurrent insert
    var retryBills = await _bills.GetByOrderIdAsync(orderId, cancellationToken);
    var retryActive = retryBills.FirstOrDefault(b => b.Status != BillState.Cancelled);
    if (retryActive != null) { ... return Map(retryActive, ...); }
    throw;
}
```

It is meant to absorb the unique-violation from a concurrent bill insert, but
it catches **any** exception (validation bug, connection fault, a defect in
`AddAsync`), then returns whatever active bill already exists for the order as a
success. The sibling `catch` immediately below (the `table_mgmt.tables` update)
is correctly narrowed to `when (ex is NpgsqlException or InvalidOperationException)`
— this one should mirror it (`catch (PostgresException { SqlState: "23505" })`).
Financial path, so error-masking here can hand back the wrong bill.

### H2 [LOW-MED] `AuditSanitizer` — value-embedded secrets in well-formed JSON

`src/Modules/Audit/EventStore/IAuditSanitizer.cs`. On well-formed JSON,
`SanitizeNode` redacts by **property name only** (`IsSensitiveKey`). A secret
inside a string *value* under a non-sensitive key —
`{"detail": "auth failed for token=eyJ..."}`,
`{"note": "pin is 4821"}` — passes through verbatim into the audit store. The
regex value-scrubbing (`FallbackSanitizeText`) runs **only** in the
`catch (JsonException)` branch. If any audit event serialises free text
(exception messages, request-context strings), a credential can land unredacted.
Fix: run the value-level regex pass over string leaves in the structured path
too, or redact any string leaf that matches a `key=value` / `key: value`
secret shape.

### H3 [LOW] `AuditSanitizer.IsSensitiveKey` — substring over-redaction

Same file. `IsSensitiveKey` does a substring match, so `pin` matches
`shipping`, `mapping`, `spinner`; `pan` matches `company`, `expand`, `plan`,
`japan`. Fields like `shipping_address`, `company_name`, `expansion_plan` have
their values replaced with `[REDACTED]` in the audit trail. Not a leak — a
loss of audit usefulness / integrity. Fix: match on token boundaries
(`_` / `-` / camelCase split) rather than raw `Contains`.

### H4 [MED] `WaiterOfflineQueueEngine` — a permanent failure blocks the queue

`src/Clients/WaiterPwa/SessionQueue/WaiterOfflineQueueEngine.cs` ~line 145. On
the first failed replay the loop `break`s (correct for FIFO) and the operation
stays queued (correct — no data loss). But `serverDispatcher` returns a bare
`bool`, so the engine cannot tell "retry later" (offline / 5xx) from "will
never succeed" (4xx validation). A permanently-rejected head-of-queue operation
therefore blocks **every** following operation indefinitely, with no
dead-letter path and nothing surfaced to the waiter beyond a generic error.
Fix: `serverDispatcher` returns retryable-vs-permanent; permanent failures move
to a dead-letter list the waiter can see and clear.

## Round 2 — money / Billing / Cash / Orders

### B1 [HIGH] V1-BIL-003 bill adjustments are fully unwired

`src/Modules/Billing/Adjustments/` ships the full domain — `AdjustmentCalculator`,
`BillAdjustment`, `IBillAdjustmentRepository`, `PostgresBillAdjustmentRepository`,
`AdjustmentEnums` — plus migration 021 `billing.bill_adjustments` and a passing
`Billing.Adjustments.Tests` (14/14). None of it is connected:

- `IBillAdjustmentRepository` / `PostgresBillAdjustmentRepository` are **not
  DI-registered** — `BillingModule.Register` wires only `IBillRepository`,
  `ISplitDesignRepository` and `TableEventBillConsumer`.
- `AdjustmentCalculator.Calculate` has **zero callers** anywhere in `src/`.
- **No Host endpoint** references adjustments — there is no way to create a
  discount / fee / tip on a bill.
- Migration 021 installs **no recalculation trigger**; no read path
  (`SplitEngine`, bill GET, any settlement flow) incorporates adjustments.

Net effect: a discount cannot be entered, and if a `bill_adjustments` row
existed it would have zero effect on `bills.payable_amount`, on any split, or
on what the customer pays. A core POS feature shipped as dead scaffolding while
`GATE-V1-EXIT` is closed. Same class as the authz wave's C1-C5, but financial.

### B2 [MED] V1-CSH-001 cash sessions are design-only

`CashModule.Register` wires only `ICashSessionPolicy -> CashSessionPolicy` (a
pure policy). There is no `ICashSessionRepository`, no aggregate, no
persistence, no `cash_session` migration, and no Host endpoint. `cash.drawer`
(authz vocabulary) has no endpoint either. The task is titled
"cash-session-**design**", so this may be a deliberate scope cut — but
`CashSessionExceptions` (`VarianceExceedsToleranceException`,
`TerminalAlreadyHasActiveSessionException`, ...) describe a runtime that does
not exist. Cash drawer / X-Z reporting / till reconciliation are non-functional
in V1.

### B3 [MED-HIGH] OrderManagement endpoints have no permission check

`src/Host/Experience/Orders/OrderManagementEndpoints.cs` — `POST /table-draft`,
`GET /table/{tableId}`, `GET /{orderId}`, `POST /{orderId}/submit` are gated by
`RequireCashierSessionAsync` (a valid cashier **session** only). There is no
`pos.cashier.mutate` / `orders.*` permission check, unlike the sibling
DualScreen order endpoints which use `RequireCashierPermissionAsync`. A second
order-creation and order-submit path that bypasses the permission model — the
class V1-GOV-028 flagged; the V1-GOV-033 fix added session auth but not a
permission gate.

### B4 [HIGH, confirm at runtime] Duplicate `orders/{id}/submit` route

`POST /api/v1/terminals/{terminalId:guid}/orders/{orderId:guid}/submit` is
registered twice in the production composition: `DualScreenApplication.Endpoints.cs`
line ~285 (`MapApi`) and `OrderManagementEndpoints.MapOrderManagementApi`
(group prefix `+ "/{orderId:guid}/submit"`). Both are mapped in
`DualScreenApplication.cs` (`MapApi(app)` then `app.MapOrderManagementApi()`).
The templates are byte-identical with equal precedence, so ASP.NET Core routing
raises `AmbiguousMatchException` (HTTP 500) on any request to that path. No test
exercises `/{orderId}/submit` through routing (`OrderManagementExperienceTests`
calls the store method directly; `ProductionExperienceCompositionTests`
de-dupes route patterns into a `HashSet`). Confirm with a live
`POST .../orders/<guid>/submit`; the two handlers also take different request
bodies (`SubmitOrderRequest` vs `SubmitTableOrderRequest`).

### B5 [LOW] `SplitEngine.CreateItemSplit` — partial groups do not remainder-balance

When an item group is not fully allocated (`totalAllocatedQty < billItem.Quantity`),
the last target for that item uses `fraction = Quantity / billItem.Quantity`
like the others instead of taking the rounding remainder. Per-target
`RoundCurrency` can then leave the allocated portion up to ~1 kuruş per target
short of the exact proportional value. The bill's own total is unaffected
(item splits are not required to sum to `PayableAmount`); this is a design-time
drift on partial item splits only.

### Round 2 — verified clean

- `BillMath.RoundCurrency` = `Math.Round(v, 2, MidpointRounding.AwayFromZero)`;
  `RoundQuantity` = 3 dp away-from-zero. Consistent throughout `SplitEngine` /
  `AdjustmentCalculator`.
- `CreateEqualSplit` / `CreateAmountSplit` / `CreateCustomSplit` are lossless —
  base amounts floored to kuruş, the remainder assigned to the last allocation,
  tax distributed by floor with the residual to the last; each enforces
  `sum == Bill.PayableAmount` (Amount / Custom) exactly.
- `AdjustmentCalculator` guards `discountGross <= basePayable + feeGross` and
  clamps adjusted tax at 0, so an adjusted payable can never go negative — the
  math is sound; it is only unreachable (B1).
- `decimal` used end to end for money and quantity; no `double` / `float` in the
  Billing money paths.

## Round 3 (partial) — wiring sweep + Kitchen

### Unwired-interface sweep across all 13 modules

Checked every `I*Repository` / `I*Service` / `I*Store` / `I*Projector` defined
in each module against its `*Module.Register` body and the Host Experience
`AddXxx` extensions. **The only genuinely unwired interface is
`IBillAdjustmentRepository`** (B1). Tables (`ITableTransfer*`, `ITableMerge*`,
`ITableReservation*`, `ITablePointerProjector`) are registered in
`AddTableManagementExperience`; `IOrderSubmissionDispatcher` is registered via a
factory in `DualScreenApplication`. Wiring discipline across the other 12
modules is sound.

### Kitchen ticket lifecycle — verified

`KitchenTicket` has explicit transition validation, `CanBeMarkedReady()`
(all non-cancelled items `Ready` / `Served`, and "all cancelled" cannot be
`Ready`), idempotent `ReadyAt` stamping (`ReadyAt ?? at`), and the auto-`Ready`
promotion (`Preparing` / `Accepted` + every non-cancelled item `Ready` /
`Served` -> ticket `Ready`) — the V1-GOV-034 fix is in place. `TicketLifecycle`
(18), `Routing` (23), `PrintQueue` (18), `PhysicalPrintRecovery` (17) tests all
green.

### Round 3 (rest) — verified clean

- **Tables transfer** — `PostgresTableTransferRepository.ExecuteTransferAsync`
  uses an explicit `ReadCommitted` transaction with canonical lock ordering
  (deadlock-safe on concurrent transfers), writes the source/target/transfer/
  audit rows and **one transactional-outbox event** in the same transaction;
  Order and Bill reparent their own rows when the outbox delivers. The
  V1-GOV-032 reparent finding is fixed.
- **Messaging** — outbox and inbox are at-least-once with exponential backoff
  and dead-letter after 3 attempts; `OutboxDispatcherHostedService` logs a
  loud operator-triage warning (EventId 5301) when anything dead-letters; the
  inbox is lease-based with lease-generation crash recovery and idempotency-key
  de-dupe. Matches V0-ARC-003. (Note: a dead-lettered reparent event has no
  automatic reconciliation beyond `PostgresTablePointerProjector` drift repair
  — operator triage is the recovery path.)
- **Vanilla-client XSS** — `cashier-app.js` and `waiter-app.js` route every
  dynamic value in their `innerHTML` templates through `escapeHtml()` (a
  `textContent` round-trip); numbers go through `Intl.NumberFormat`. The
  V1-GOV-028 XSS hardening is complete. Non-escaped interpolations are all
  `fetch()` URLs / headers or `.textContent` / `alert` sinks.

## Consolidated remediation order (all rounds)

Fold into the Phase 0 `V1-GOV-071` cluster alongside the authz-wave work.
B7 and B2 are **not** on this list — they are V1.5 / V1.2 scope (recorded); the
only decision they carry is a labelling note for the `V1-GOV-072` reseal (V1 =
operational core, not a feature-complete POS).

1. **B4 [HIGH]** — confirm the duplicate `orders/{id}/submit` route with a live
   `POST`, then remove one registration. Decide the single owner of the order
   HTTP surface (DualScreen vs `OrderManagementEndpoints`) and delete the other
   path; do not leave two front doors.
2. **B1 [MED-HIGH]** — resolve the bill-adjustments scope: `V1-BIL-003` says
   "persist" and has no V1.5 handoff, so either DI-register
   `IBillAdjustmentRepository`, add a create-adjustment endpoint and apply
   `AdjustmentCalculator.Calculate` to the bill / settlement, or add an explicit
   V1.5 handoff and note the deferral like the other foundations.
3. **B3 [MED]** — decide whether `OrderManagementEndpoints` mutating routes need
   a permission gate on top of the session check (they are session-only today),
   and remove the module in favour of the DualScreen order endpoints if (1)
   makes it redundant.
4. **H1 [MED]** — narrow `BillingSplitStore.CreateBillFromOrder`'s
   `catch (Exception)` to the unique-violation only, mirroring the sibling
   `catch`.
5. **H4 [MED]** — `WaiterOfflineQueueEngine`: make `serverDispatcher` return
   retryable-vs-permanent; permanent (4xx) failures move to a visible
   dead-letter list instead of blocking the queue forever.
6. **H2 [LOW-MED]** — `AuditSanitizer`: run the value-level regex over string
   leaves on the well-formed-JSON path, not only the malformed fallback.
7. **H3 [LOW]** — `AuditSanitizer.IsSensitiveKey`: token-boundary match instead
   of raw `Contains` (stop redacting `shipping`, `company`, ...).
8. **B5 [LOW]** — `SplitEngine.CreateItemSplit`: give the last target of a
   partially-allocated item the rounding remainder.
9. **B6 [LOW]** — add `row_version` to `catalog.products` /
   `catalog.product_prices` and optimistic-concurrency checks in the catalog
   repositories.
10. **KVKK [process]** — schedule `kvkk-retention --apply` and `housekeeping`
    (cron / systemd timer / operator runbook); they exist but are manual.

Not on this list (recorded scope, no code owed in the V1 remediation wave):
**B7** — the five foundation modules pick up in V1.5 (`V15-RPT-001`,
`V15-OBS-002`, `V15-REC-001/002`, `V15-NOT-001`). **B2** — cash is `V1-CSH-001`
"design for V1.2, without payment"; leave `cash.drawer` in the vocabulary or
remove it in V1-IAM-024 — a one-line call, not a remediation task.

## Round 4 — Experience-layer atomicity — verified clean

Every multi-table write goes through a transactional module repository:

- `PostgresTableFloorPlanRepository.SaveAsync` — `Serializable` transaction over
  floor plan + layouts + seats (delete-then-insert), with a version check.
- `PostgresSplitDesignRepository.ReplaceOperationalSplitDesignAsync` —
  transaction with `LockBillAsync` (row lock), quantity + seat-owner validation,
  delete + insert allocations, commit.
- `PostgresTableTransferRepository.ExecuteTransferAsync` — see Round 3.

Single-row Experience writes (`KitchenOperationsStore` transitions,
`ZoneConcurrencyStore` zone CRUD, `CatalogManagementStore` creates) are one
statement each and carry optimistic concurrency (`row_version` /
`ExpectedRowVersion`). The only non-atomic Experience write is
`BillingSplitStore.CreateBillFromOrder` (bill insert then a separate
`table_mgmt.tables` pointer update) — that is H1 plus a documented
soft-cache-with-drift-repair design.

## Round 5 — migrations 001-042 — mostly clean

- 47 / 47 migrations have a `.down.sql`; the full chain applies forward and
  reverses to empty.
- All financial tables (`billing.bills`, `bill_items`, `bill_allocations`,
  `bill_adjustments`) carry `row_version`; `audit.audit_events` has
  `BEFORE UPDATE OR DELETE` immutability triggers; `orders.*`, `kitchen_tickets`,
  `settings`, `reconciliation.*`, `table_mgmt.*` carry `row_version`.
- **B6 [LOW]** `catalog.products` and `catalog.product_prices` (migrations 006,
  007) have no `row_version` — concurrent manager edits to the same product or
  price are silently last-write-wins with no conflict detection. Optimistic
  concurrency on the catalog config tables would match the rest of the schema.
- Config tables (`identity.permissions` / `roles` / `role_permissions`,
  `printer_routes`, projection tables in `reporting.*`) without `row_version`
  are acceptable — low concurrency, rebuildable, or intentionally last-write.

## Audit status

Rounds 1-5 complete: baseline, money / Billing / Cash / Orders, the
unwired-interface sweep, Kitchen lifecycle, Tables transfer, messaging,
vanilla-client XSS, Experience atomicity, migrations. Findings: H1-H4 (Round 1),
B1-B6 (Rounds 2-5) — two HIGH (B1 adjustments unwired, B4 duplicate submit
route), one MED-HIGH (B3 Orders permission gap), the rest MED / LOW. Everything
else spot-checked across all 13 modules, the Host, the three clients and the
migration set is sound — consistent with a surface already through ~25 audit
cycles.

Not yet done (would be Rounds 6+, lower expected yield): line-by-line review of
each module's remaining domain code (Observability, Alerts, Operations,
Reconciliation, Reporting, Settings internals), the PosTerminal React
route / effect graph in full, every Experience endpoint's exception-path
completeness, the `deploy/docker/**` TLS / proxy split, KVKK retention +
housekeeping internals, and a formal per-endpoint threat model. These are
follow-on passes; the high-risk classes are covered above.

## Round 6 — module reachability, KVKK, deploy

### CORRECTION — B7 and B2 cross-referenced against the V1 -> V1.5 roadmap

The initial B7 / B2 write-up below was drafted before cross-referencing the
plan's own roadmap. That cross-reference materially changes them:

- Every module in B7 is a task literally named **`*-foundation`**
  (`V1-ALT-001` "Alert **foundation**", `V1-OBS-001` "observability ...
  **foundation**", `V1-OPS-001` "audit **foundation**", `V1-REC-001`
  "ReconciliationCase **foundation**", `V1-RPT-001` "operational-report
  **foundation**", `V1-SET-001` "typed-settings"). Their `Handoff` targets are
  all **V1.5** tasks, and those pickup tasks exist:
  `plan/v1.5/reporting/V15-RPT-001-consolidated-reporting.md`,
  `plan/v1.5/observability/V15-OBS-002-health-and-alerts.md`,
  `plan/v1.5/reconciliation/V15-REC-001-unified-reconciliation-read-model.md`
  (+ `V15-REC-002`), `plan/v1.5/notifications/V15-NOT-001-notification-delivery.md`.
- `GATE-V1-EXIT` was deliberately closed by `V1-GOV-051` (2026-09-01) on the
  **operational core** (login -> menu -> table draft -> order submit -> active
  poll -> kitchen -> bill -> split), with a load-test baseline as the final
  gate. The reporting / settings / alerts / reconciliation **surfaces** were
  scoped to V1.5 by design.
- `V1-CSH-001` is titled "Finalize CashSession **design for V1.2**" and is
  scoped "without enabling payment".

So **B7 and B2 are not defects** — they are recorded scope boundaries: V1 ships
the domain foundations, V1.5 ships the API / dashboards / notifications, V1.2
enables cash + payment. The one legitimate residual is **B1** (bill adjustments)
— its task `V1-BIL-003` says "calculate **and persist**", has **no V1.5 handoff**
and no pickup task, yet nothing applies an adjustment to a bill or exposes it.
That is a genuine scope ambiguity, not a clean deferral.

The pre-existing deferral roadmap is itself recorded in
`plan/REMAINING_WORK_PLAN.md` (a 2026-08-12 snapshot — now stale, most of its
"Planned" list is Done or belongs to V1.5-V20) and in the per-task V1.5
`Handoff` chain.

### B7 (as first drafted — now reframed) Five V1 modules are foundation-only

Each is registered in `ModuleRegistry.DefaultCatalog`, wires its repositories
and services in `*Module.Register`, has passing unit tests and a migration that
creates its tables — and **nothing calls it**: no Host endpoint references its
services, and no hosted service / background service / integration-event
consumer / projector / other module consumes it.

- **V1-RPT-001 Reporting** — `OperationalReportService` +
  `reporting.daily_business_days` / `waiter_performance_summaries` /
  `print_error_summaries`. No endpoint reads a report; migration 031 installs
  **no trigger, projector or job** to populate the tables. V1 ships with no
  operational reporting — no X / Z, no daily business day, no waiter
  performance.
- **V1-SET-001 Settings** — `SettingsService` + `SettingValidator` +
  `settings` / `setting_history`. No endpoint, and no module reads a setting
  (`PasswordHasher` uses a hardcoded `DefaultIterations = 600_000`, not the
  settings store). V1 has no runtime configuration surface.
- **V1-REC-001 Reconciliation** — `ReconciliationService` +
  `reconciliation.cases` / `case_actions`. Nothing creates a case; no endpoint
  views one. Inert.
- **V1-ALT-001 Alert foundation** — `AlertService` + `alerts` / `alert_events`.
  Zero alert producers anywhere in `src/`; no endpoint. Inert.
- **Observability health-check module** — `IHealthCheckRepository` /
  `IObservabilityService` are unused; `/health/ready` calls
  `DualScreenStore.CheckReadyAsync` directly, not this module.

**Reframed (see the correction above):** this is the intended V1 -> V1.5 split,
not an accident. The residual observation worth carrying to the reseal: closing
`GATE-V1-EXIT` on the operational core while calling the matrix "219 Done" can
read as "V1 is a complete POS" when reporting, runtime settings, alerting and
reconciliation have no surface until V1.5. That is a labelling / expectations
point for `V1-GOV-072`, not a code defect.

### KVKK retention — implemented, manual

`Program.cs kvkk-retention --db-url <url> [--apply] [--as-of <date>]` anonymizes
`orders.notes` / `order_items.notes` / `table_reservations.reason` and disables
staff `password_hash` past the V0-CMP-003 retention windows; it is idempotent
and defaults to a dry run. It is an **operator-invoked CLI verb**, not a
scheduled job — KVKK compliance depends on ops running it (the full
partition-aware version is deferred to `V15-KVK-001`). `housekeeping` (expired
idempotency keys + long-revoked device sessions) is the same shape.

### Deploy — verified clean

`deploy/docker/` — Caddy terminates TLS with an internal CA, `default_sni`
pinned to `ALKAROS_PROXY_HOST` for bare-IP LAN clients, strips the inbound
`X-Alkaros-Origin` header before proxying (the customer-display origin signal
must originate at the proxy), gzip, SPA deep-link fallback; `pg_hba.conf`,
tuned `postgresql.conf`, PITR + basebackup + restore self-check scripts present;
secrets are gitignored with `.example` templates. Hardened by V1-RMD-096 / 098,
V1-GOV-069 / 070.

### PosTerminal — consistent

All six `src/features/*` directories are routed in `workspace.tsx`
(`/tables`, `/billing`, `/catalog`, `/kitchen`, `/system-health`,
`/authorization`). No dead feature directory; no reporting / settings screen
exists (consistent with the inert backend modules — nothing broken, just
absent).

## Audit status (updated, after the roadmap cross-reference)

Rounds 1-6 complete. **Genuine defects:**

- **B4 [HIGH, confirm at runtime]** — duplicate `orders/{id}/submit` route ->
  `AmbiguousMatchException`. Not a scope decision; the V1-GOV-051 load test
  exercised `GET catalog` / `GET orders/active` / `POST table-draft`, never
  `/{orderId}/submit` through routing, so it would not have caught this.
- **B1 [MED-HIGH]** — bill adjustments: `V1-BIL-003` says "calculate and
  persist", has no V1.5 handoff, yet no path applies an adjustment to a bill
  or exposes it. Scope-ambiguous rather than a clean deferral.
- **B3 [MED]** — `OrderManagementEndpoints` mutating routes are session-only.
  V1-GOV-033 added the session check deliberately; whether a permission gate is
  also required is the open question. Downgraded from MED-HIGH.
- **H1 [MED]** — `BillingSplitStore.CreateBillFromOrder` broad `catch (Exception)`.
- **H4 [MED]** — `WaiterOfflineQueueEngine` permanent-failure queue block.
- **H2 [LOW-MED] / H3 [LOW]** — `AuditSanitizer` value-embedded secrets /
  over-redaction.
- **B5 [LOW]** — partial item-split rounding drift.
- **B6 [LOW]** — `catalog.products` / `product_prices` have no `row_version`.

**Not defects — recorded scope boundaries:** B7 (Reporting / Settings /
Reconciliation / Alerts / health-check are V1 foundations; surfaces are V1.5 —
`V15-RPT-001`, `V15-OBS-002`, `V15-REC-001/002`, `V15-NOT-001`), B2 (cash is
`V1-CSH-001` "design for V1.2, without payment"). Carry the "V1 core vs full
POS" labelling point to `V1-GOV-072`.

Everything spot-checked across the money paths, Tables, Kitchen, messaging,
Experience atomicity, migrations, vanilla-client XSS and the Docker deploy is
sound — consistent with a surface through ~25 audit cycles plus a load-test
gate.

## Rounds 7+ — not yet covered (the map)

Each is its own focused pass; suggested order by risk.

1. **Money math** — `SplitEngine`, `AdjustmentCalculator`, tax / discount / fee
   interaction, rounding direction and residual allocation, `decimal`
   discipline end to end (Billing, Cash, Reporting).
2. **Billing / Cash domain** — `Bill` state machine, `bill_allocations` /
   `bill_adjustments` invariants, `CashSession` variance / override, optimistic
   concurrency on every financial write, DB-level immutability triggers on
   `billing.*` / `cash.*`.
3. **Orders** — `Order` aggregate state machine, the "second unauthorised order
   path" class (V1-GOV-028), `OrderManagementEndpoints` vs the DualScreen order
   endpoints (two front doors), submit idempotency.
4. **Tables** — lifecycle / transfer / merge / unmerge, `ZoneConcurrencyStore`,
   `PostgresTablePointerProjector` drift repair, floor-plan save atomicity,
   reservation expiry.
5. **Kitchen** — ticket + item state machine, auto-`Ready` promotion
   (V1-GOV-034), print queue + physical-print recovery claim races, routing
   fallback, reprint approval authority.
6. **Experience layer** — every endpoint's full exception-path mapping, status
   codes, atomicity of multi-write handlers, rate limiting coverage, the
   `AllowedCommands` contract per surface.
7. **Integration / messaging** — outbox at-least-once + de-dupe, inbox lease /
   generation, idempotent consumers, the table-merge reparent fan-out,
   `TransactionOutboxIntegration`.
8. **Clients** — PosTerminal (React) route + effect review, contract drift vs
   the Host DTOs, error / offline UX; WaiterPwa engines; Cashier engines;
   the three `wwwroot/*.js` bundles for XSS / DOM injection.
9. **Migrations 001-042** — per-table constraint / index / trigger completeness,
   FK cascade correctness, `phaseBRange` and deferred-constraint declarations.
10. **Cross-cutting** — `PayloadRedactor` / observability redaction, KVKK
    retention + housekeeping, secrets handling, CSP + security headers, TLS /
    proxy split (`deploy/docker/**`), rate-limiter partitions.
11. **Provisioning / bootstrap** — `Program.cs provision-manager`, role / user
    seeding for `waiter` / `cashier` / `supervisor` (see also
    `authz-wave-remediation-plan.md` P1).

## How to run the remaining rounds

Pick one row, read every file in that area, trace the main flow end to end,
run that area's test project plus a targeted adversarial-input review, append
findings here with a severity and a concrete fix. When a round produces
actionable defects, register them under a `V1-GOV-*` remediation cluster the
same way the authz wave's Phase 0 does.

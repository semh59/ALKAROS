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

## Rounds 2+ — not yet covered (the map)

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

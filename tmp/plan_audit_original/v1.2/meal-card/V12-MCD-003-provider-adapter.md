# V12-MCD-003 - Implement approved meal-card provider adapter

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.2 scope plus validated provider contract and audit corrections.

## Goal

Implement payment, unknown query, cancel/refund and statement ingestion only for the provider validated in V0.

## Owned surface

- `src/Modules/MealCard/Providers/ApprovedProvider/**`, `tests/Modules/MealCard/Providers/ApprovedProvider/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Official request/response mapping, credentials, idempotency, unknown state, settlement statement and sanitized evidence.

## Out of scope

- Unvalidated providers and customer-account behavior.

## Dependencies

- V0-MCD-001,V12-MCD-001,V12-MCD-002,V12-ALC-003

## Deliverables

- V12-MCD-003 için production implementation.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Provider sandbox/contract tests cover approved, declined, timeout/query, refund and statement match; no fake success path exists.

## Handoff

- V12-REC-001 and V15-REC-001.


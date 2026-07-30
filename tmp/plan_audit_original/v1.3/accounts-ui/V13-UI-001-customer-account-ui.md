# V13-UI-001 - Implement customer and account UI

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF scope plus V0 compliance/domain correction; conditional behavior requires recorded evidence.

## Goal

Implement customer profile, account ledger, balance/aging and account payment screens under field permissions.

## Owned surface

- `src/Clients/Cashier/CustomerAccounts/**`, `tests/Clients/Cashier/CustomerAccounts/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- PII access, ledger view, Bill charge, account payment, anonymization status and stale balance indicator.

## Out of scope

- Invoice submission and privacy batch execution.

## Dependencies

- V13-CST-001,V13-ACC-002,V13-ACC-003,V13-ACC-004,V1-IAM-002

## Deliverables

- V13-UI-001 için production implementation.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Unauthorized PII is hidden server-side; displayed balance matches projection rebuild; repeated payment submit has one effect.

## Handoff

- V13-UI-002.


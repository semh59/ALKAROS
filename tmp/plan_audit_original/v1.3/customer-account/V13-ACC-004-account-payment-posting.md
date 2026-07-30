# V13-ACC-004 - Implement customer account payment posting

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF scope plus V0 compliance/domain correction; conditional behavior requires recorded evidence.

## Goal

Post a customer payment/credit independently of a restaurant Bill and update the balance projection once.

## Owned surface

- `src/Modules/CustomerAccounts/Payments/**`, `tests/Modules/CustomerAccounts/Payments/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Payment source/reference, positive magnitude, allocation to open receivable policy, idempotency and audit.

## Out of scope

- Restaurant Bill charge and provider payment transport.

## Dependencies

- V13-ACC-001,V13-ACC-002,V0-DOM-007

## Deliverables

- V13-ACC-004 için production implementation.
- Public contract/UI ve otomatik success/failure/idempotency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Repeated receipt creates one ledger entry; balance reduces by exact amount; overpayment behavior follows approved policy rather than implicit clipping.

## Handoff

- V13-UI-001 and V13-RPT-001.


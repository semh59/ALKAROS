# V12-HUG-002 - Implement Hugin unknown-state recovery

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Handle timeout/connection loss as Unknown and query/reconcile to one authoritative terminal result.

## Owned surface

- `src/Modules/Payments/Hugin/UnknownRecovery/**`, `tests/Modules/Payments/Hugin/UnknownRecovery/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Timeout classification, status query, retry limits, unresolved case creation and late result handling.

## Out of scope

- New payment request and refund execution.

## Dependencies

- V12-HUG-001,V0-HUG-001

## Deliverables

- V12-HUG-002 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Timeout never becomes implicit decline/success; terminal query resolves once or opens a reconciliation case.

## Handoff

- V12-REC-001.


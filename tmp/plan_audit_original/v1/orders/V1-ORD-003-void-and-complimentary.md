# V1-ORD-003 - Implement item void and complimentary commands

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Apply approved void/complimentary policy with permission, reason, audit and kitchen-state checks.

## Owned surface

- `src/Modules/Orders/ItemExceptions/**`, `tests/Modules/Orders/ItemExceptions/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Pre/post-kitchen eligibility, zero-price result, audit and downstream event.

## Out of scope

- Refund, waste stock effect and discount calculation.

## Dependencies

- V1-ORD-001,V1-IAM-002,V1-OPS-001,V0-DOM-006

## Deliverables

- V1-ORD-003 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Unauthorized/late void fails; complimentary preserves delivered quantity and tax/fiscal inputs required by policy.

## Handoff

- V11-RSV-003 and V1-BIL-003.


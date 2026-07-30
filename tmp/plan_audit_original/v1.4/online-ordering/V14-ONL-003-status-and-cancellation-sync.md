# V14-ONL-003 - Implement online status and cancellation synchronization

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Send/receive provider status and cancellation changes with race-safe local transition rules.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/StatusSync/**`, `tests/Modules/OnlineOrdering/Yemeksepeti/StatusSync/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Outbound idempotency, late cancellation, already-preparing policy, retry and provider/local conflict.

## Out of scope

- Webhook intake and product mapping.

## Dependencies

- V14-ONL-002,V14-MAP-002,V0-YSP-001

## Deliverables

- V14-ONL-003 için production implementation.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Duplicate status call has one effect; cancellation race resolves deterministically and any divergence opens reconciliation.

## Handoff

- V14-REC-001.


# V14-REC-001 - Implement online order reconciliation

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Detect and track local/provider order, status, cancellation and stock outcome divergence.

## Owned surface

- `src/Modules/Reconciliation/OnlineOrders/**`, `tests/Modules/Reconciliation/OnlineOrders/**`, `database/migrations/V14/V14-REC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Paired references, open-case deduplication, retry action and audited resolution.

## Out of scope

- Unified dashboard and generic reconciliation lifecycle.

## Dependencies

- V14-ONL-003,V14-STK-001,V12-REC-001

## Deliverables

- V14-REC-001 için production implementation.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Provider accepted/local rejected and local accepted/provider unknown each create one case with safe next action.

## Handoff

- V15-REC-001 and V15-REC-002.


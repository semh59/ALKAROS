# V11-INV-005 - Implement manual inventory adjustment

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.1 module/schema sections plus named correction dependency.

## Goal

Post permissioned Adjustment movements with mandatory reason and audit without editing balances directly.

## Owned surface

- `src/Modules/Inventory/ManualAdjustments/**`, `tests/Modules/Inventory/ManualAdjustments/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Increase/decrease direction, non-negative result, permission, reason and idempotency.

## Out of scope

- Waste, returns and purchase receipt.

## Dependencies

- V11-INV-001,V11-INV-002,V1-IAM-002,V1-OPS-001

## Deliverables

- V11-INV-005 için production implementation.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Unauthorized or negative-result adjustment fails; successful adjustment produces one movement and matching projection.

## Handoff

- V11-UI-003 and V11-RPT-001.


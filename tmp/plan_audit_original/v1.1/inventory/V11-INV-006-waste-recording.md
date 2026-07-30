# V11-INV-006 - Implement general waste recording

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.1 module/schema sections plus named correction dependency.

## Goal

Post traceable Waste movements from production, portion reservation or manual approved source.

## Owned surface

- `src/Modules/Inventory/WasteRecording/**`, `tests/Modules/Inventory/WasteRecording/**`, `database/migrations/V11/V11-INV-006/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Typed source, quantity/unit, reason, permission, audit and balance effect.

## Out of scope

- Payment refund, manual adjustment and cancellation classification.

## Dependencies

- V11-INV-001,V11-INV-002,V1-IAM-002,V1-OPS-001

## Deliverables

- V11-INV-006 için production implementation.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Each waste record links one source and movement; duplicate submit has one effect; prepared product never returns to available.

## Handoff

- V11-RPT-001.


# V15-KVK-001 - Implement KVKK retention execution

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Evaluate the approved data inventory and schedule eligible deletion/anonymization actions across all stores.

## Owned surface

- `src/Modules/Privacy/RetentionExecution/**`, `tests/Modules/Privacy/RetentionExecution/**`, `database/migrations/V15/V15-KVK-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Policy version, due selection, legal hold, dry run, idempotency and audit.

## Out of scope

- Field-level anonymization implementation.

## Dependencies

- V0-CMP-003,V13-CST-002,V15-SEC-003

## Deliverables

- V15-KVK-001 için production implementation veya executable test asset.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Dry-run and execution select the same eligible records; legal hold blocks mutation; repeated run is stable.

## Handoff

- V15-KVK-002 and V20-CMP-001.

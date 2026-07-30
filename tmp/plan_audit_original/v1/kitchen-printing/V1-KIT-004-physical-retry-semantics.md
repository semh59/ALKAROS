# V1-KIT-004 - Implement physical print retry safeguards

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Handle the send/ack crash window with explicit uncertain state and operator-controlled reprint semantics.

## Owned surface

- `src/Modules/Kitchen/PhysicalPrintRecovery/**`, `tests/Modules/Kitchen/PhysicalPrintRecovery/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Uncertain delivery, reprint labeling, operator acknowledgement and duplicate-risk audit.

## Out of scope

- Logical queue creation and printer configuration.

## Dependencies

- V1-KIT-003,V0-PRN-001

## Deliverables

- V1-KIT-004 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Crash after device send but before local commit never auto-claims exactly-once; recovery requires explicit safe policy and is audited.

## Handoff

- V1 exit gate and V15-RUN-001.


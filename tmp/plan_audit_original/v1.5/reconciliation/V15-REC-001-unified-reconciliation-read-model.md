# V15-REC-001 - Implement unified reconciliation read model

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Build one read model for open cases across payment, fiscal, QNB, online, meal card, cash and purchasing.

## Owned surface

- `src/Modules/Reconciliation/DashboardReadModel/**`, `tests/Modules/Reconciliation/DashboardReadModel/**`, `database/migrations/V15/V15-REC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Source pair display, severity, age, ownership, next action and rebuild.

## Out of scope

- Case resolution commands and alert delivery.

## Dependencies

- V12-REC-001,V13-QNB-004,V13-PUR-001,V14-REC-001,V0-DAT-004

## Deliverables

- V15-REC-001 için production implementation veya executable test asset.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Read model rebuild reproduces case counts; no case type loses source references or required next action.

## Handoff

- V15-REC-002 and V15-OBS-002.


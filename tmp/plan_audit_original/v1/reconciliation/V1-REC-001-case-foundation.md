# V1-REC-001 - Implement ReconciliationCase foundation

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Implement canonical case lifecycle, paired source references, open-case deduplication and append-only events/actions.

## Owned surface

- `src/Modules/Reconciliation/CaseFoundation/**`, `tests/Modules/Reconciliation/CaseFoundation/**`, `database/migrations/V1/V1-REC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Case identity, source pair, status transitions, severity, reason and uniqueness.

## Out of scope

- Payment/QNB/online-specific detectors and dashboard.

## Dependencies

- V1-FND-001,V0-DOM-001,V0-DAT-002

## Deliverables

- V1-REC-001 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Same mismatch key creates one open case; forbidden transitions fail; history cannot update/delete.

## Handoff

- V12-REC-001, V13-QNB-004, V14-REC-001 and V15-REC-001.


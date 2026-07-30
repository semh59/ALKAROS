# V1-ALT-001 - Implement Alert foundation

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Implement canonical alert lifecycle, source reference, deduplication and acknowledgement audit.

## Owned surface

- `src/Modules/Observability/AlertFoundation/**`, `tests/Modules/Observability/AlertFoundation/**`, `database/migrations/V1/V1-ALT-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Info/Warning/Critical severity, lifecycle, dedup key and source catalog.

## Out of scope

- Notification delivery, escalation schedule and health evaluation.

## Dependencies

- V1-FND-001,V0-DOM-001,V0-DAT-002

## Deliverables

- V1-ALT-001 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Repeated same source/error yields one active alert; acknowledgement/resolution actor and timestamps are preserved.

## Handoff

- V15-OBS-002 and V15-NOT-001.


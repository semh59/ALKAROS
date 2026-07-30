# V1-OBS-001 - Implement observability correlation foundation

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Add structured event contracts, correlation/request IDs and bounded health check persistence for V1 flows.

## Owned surface

- `src/Modules/Observability/Foundation/**`, `tests/Modules/Observability/Foundation/**`, `database/migrations/V1/V1-OBS-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Correlation propagation, core health status catalog, redaction hook and persisted retention-policy identity driven by the approved policy.

## Out of scope

- Full alert rules, metrics backend and sensitive payload encryption.

## Dependencies

- V1-FND-001,V0-DAT-002,V0-CMP-003

## Deliverables

- V1-OBS-001 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Order submit to print queue is traceable by one correlation ID; health status is canonical; secret test marker is redacted; persistence without an approved retention-policy identity is rejected.

## Handoff

- V15-OBS-001, V15-OBS-002 and V15-OBS-003.

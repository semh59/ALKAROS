# V1-OPS-001 - Implement append-only audit foundation

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Persist immutable audit events for V1 critical commands with actor, reason, correlation and before/after references.

## Owned surface

- `src/Modules/Audit/EventStore/**`, `tests/Modules/Audit/EventStore/**`, `database/migrations/V1/V1-OPS-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Append API, database write restrictions, sensitive-field filtering and core V1 command integration.

## Out of scope

- Tamper-evident external archive and KVKK anonymization execution.

## Dependencies

- V1-FND-001,V1-IAM-002,V0-CMP-003

## Deliverables

- V1-OPS-001 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Ordinary application roles cannot update/delete audit rows; denied sensitive fields never enter before/after payloads.

## Handoff

- V15-SEC-003 and V15-KVK-002.


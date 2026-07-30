# V15-OBS-003 - Implement observability retention and partitioning

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Bound growth of health, alert-event, inbox/outbox and high-volume audit-support data without deleting protected records.

## Owned surface

- `src/Modules/Observability/Retention/**`, `tests/Modules/Observability/Retention/**`, `database/migrations/V15/V15-OBS-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Partition keys, retention classes, purge jobs, legal holds and purge audit.

## Out of scope

- Customer PII anonymization and immutable financial/audit events.

## Dependencies

- V15-OBS-001,V15-OBS-002,V0-CMP-003

## Deliverables

- V15-OBS-003 için production implementation veya executable test asset.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Retention test removes only expired eligible partitions/rows; held or immutable data remains; job is restart-safe.

## Handoff

- V20-MIG-001.


# V1-KIT-003 - Implement persistent print queue

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Persist one logical PrintJob per ticket/output and process retries through the idempotency infrastructure.

## Owned surface

- `src/Modules/Kitchen/PrintQueue/**`, `tests/Modules/Kitchen/PrintQueue/**`, `database/migrations/V1/V1-KIT-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Canonical status transitions, attempts, backoff, ownership lease, restart recovery and logical deduplication.

## Out of scope

- Physical printer ambiguity after an unacknowledged send.

## Dependencies

- V1-KIT-001,V1-KIT-002,V1-FND-002,V0-PRN-001

## Deliverables

- V1-KIT-003 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Restart resumes pending work; duplicate enqueue yields one logical job; failed print never deletes order/ticket data.

## Handoff

- V1-KIT-004.


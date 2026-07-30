# V1-FND-002 - Implement the idempotency infrastructure

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement request-key validation, response replay, inbox persistence and outbox dispatch contract from V0-ARC-003.

## Owned surface

- `src/BuildingBlocks/Idempotency/**`, `src/BuildingBlocks/Messaging/**`, `tests/BuildingBlocks/Idempotency/**`, `database/migrations/V1/V1-FND-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Same-key/same-body replay, same-key/different-body rejection, inbox uniqueness and recoverable outbox dispatch.

## Out of scope

- Order-specific submit rules and provider payload mapping.

## Dependencies

- V1-FND-001,V0-ARC-003

## Deliverables

- V1-FND-002 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Automated tests prove replay semantics and process restart does not lose pending outbox records.

## Handoff

- V1-ORD-002, V1-KIT-003 and V14-ONL-001.


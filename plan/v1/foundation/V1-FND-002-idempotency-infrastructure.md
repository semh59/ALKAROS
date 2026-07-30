# V1-FND-002 - Implement the idempotency infrastructure

- Task ID: V1-FND-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.11-I.15
- CORR:C4

## Goal

V0-ARC-003 sözleşmesindeki request-key validation, response replay, Inbox persistence ve Outbox dispatch altyapısını
uygulamak.

## Owned surface

- `src/BuildingBlocks/Idempotency/**`, `src/BuildingBlocks/Messaging/**`, `tests/BuildingBlocks/Idempotency/**`,
  `database/migrations/V1/V1-FND-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Same-key/same-body replay, same-key/different-body rejection, Inbox uniqueness ve recoverable Outbox dispatch.

## Out of scope

- Order'ye özel gönderme kuralları ve provider yük eşlemesi.

## Dependencies

- V1-FND-001
- V1-SEC-002
- V0-ARC-003

## Deliverables

- `src/BuildingBlocks/Idempotency/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Automated testler replay semantiğini ve process restart'ın pending Outbox kayıtlarını kaybetmediğini kanıtlar.

## Handoff

- V1-FND-006
- V1-ORD-002
- V1-KIT-003
- V14-ONL-001

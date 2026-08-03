# V0-GOV-014 - Apply exponential messaging retry backoff

- Task ID: V0-GOV-014
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Inbox ve Outbox retry state'inde her basarisiz denemeden sonraki gecikmeyi
belirlenmis exponential backoff kuraliyla kalici olarak hesaplamak.

## Owned surface

- `src/BuildingBlocks/Messaging/RetryPolicy.cs`
- `tests/BuildingBlocks/Idempotency/RetryPolicyTests.cs`
- `tests/BuildingBlocks/Idempotency/RetryScheduleIntegrationTests.cs`
- `evidence/V0-GOV-014/**`

## In scope

- Mevcut base delay ve MaxAttempts kuraliyla PostgreSQL retry timestamp
  hesaplamasi ve birinci/ikinci hata integration testleri.

## Out of scope

- Batch scheduling, handler davranisi, dead-letter threshold, schema, yeni
  retry policy secenegi veya provider retry davranisi.

## Dependencies

- V0-GOV-012
- V1-FND-002

## Deliverables

- Retry timestamp'ini deneme sayisina gore exponential hesaplayan tek SQL
  guncellemesi ve integration test kaniti.

## Acceptance evidence

- Ilk hata 1x, ikinci hata 2x base delay sonrasi due olur; ucuncu hata dead
  durumuna gecer ve retry timestamp'i kalmaz.
- Idempotency test projesi basariyla tamamlanir.

## Handoff

- V1-FND-006

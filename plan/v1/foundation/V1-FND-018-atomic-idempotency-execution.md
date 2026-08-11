# V1-FND-018 - Independently verify atomic idempotent execution

- Task ID: V1-FND-018
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

aynı idempotency key için protected mutation ve terminal outcome davranışını deterministic concurrency/crash-replay probe ile yeniden üretmek ve atomiklik sonucunu bağımsız doğrulamak.

## Owned surface

- `src/BuildingBlocks/Idempotency/IdempotencyKeyStore.cs`
- `tests/BuildingBlocks/Idempotency/IdempotencyKeyStoreTests.cs`
- `evidence/V1-FND-018/**`

## In scope

- `CODE-003` için idempotency claim/in-progress/completed state machine'ini protected mutation transaction sınırına bağlamak.
- Concurrent claim ve crash-replay yollarını task-owned testlerle doğrulamak.

## Out of scope

- Owned surface dışındaki messaging, migration, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-FND-002
- V1-FND-012

## Deliverables

- Atomic execution implementation diff'i, concurrency/crash-replay tests ve raw transcript.

## Acceptance evidence

- Aynı idempotency key protected mutation'ı en fazla bir kez uygular; replay terminal outcome'ı döndürür.
- Focused tests ve `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.

## Handoff

- V0-GOV-045

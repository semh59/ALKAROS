# V1-FND-019 - Independently verify message lease fencing

- Task ID: V1-FND-019
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

inbox/outbox stale lease worker'ının acknowledgement, retry veya terminal success yazamadığını generation/fencing interleaving'iyle bağımsız doğrulamak.

## Owned surface

- `src/BuildingBlocks/Messaging/InboxStore.cs`
- `src/BuildingBlocks/Messaging/InboxMessage.cs`
- `src/BuildingBlocks/Messaging/RetryPolicy.cs`
- `tests/BuildingBlocks/Idempotency/InboxStoreTests.cs`
- `tests/BuildingBlocks/Idempotency/InboxRedeliveryContractTests.cs`
- `tests/BuildingBlocks/Idempotency/RetryPolicyTests.cs`
- `evidence/V1-FND-019/**`

## In scope

- `CODE-004;CODE-005;CODE-018` için lease-token fencing, affected-row enforcement ve sanitized persisted error finalization'ını tek messaging integration diff'inde uygulamak.
- Stale-worker, zero-row retry ve secret-bearing handler failure interleavinglerini task-owned testlerle doğrulamak.

## Out of scope

- Owned surface dışındaki Idempotency contract, migration, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-FND-002
- V1-FND-012
- V1-FND-018
- V1-FND-014
- V1-FND-015

## Deliverables

- Tek message-finalization integration diff'i, concurrency/security regression tests ve raw transcript.

## Acceptance evidence

- Stale lease owner acknowledgement/retry/terminal write yapamaz; affected-row count `1` değilse success sayılmaz.
- Persisted handler error bounded/allowlisted olur ve raw secret/PII taşımaz.
- Focused tests ve `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.

## Handoff

- V0-GOV-045

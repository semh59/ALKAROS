# V1-FND-020 - Independently verify rollback exhaustion

- Task ID: V1-FND-020
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

bir rollback callback'i hata verdiğinde bütün kayıtlı rollback kaynaklarının yine de tam bir kez denendiğini ve
hataların korunduğunu bağımsız doğrulamak.

## Owned surface

- `src/BuildingBlocks/Transactions/TransactionScope.cs`
- `tests/BuildingBlocks/Transactions/Execution/TransactionExecutionTests.cs`
- `evidence/V1-FND-020/**`

## In scope

- `CODE-015` için bütün rollback resource'larının CancellationToken.None ile denenmesini ve bütün hataların aggregate
  edilmesini uygulamak.

## Out of scope

- Owned surface dışındaki transaction, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-FND-011

## Deliverables

- Rollback exhaustion implementation diff'i, failure aggregation testleri ve raw transcript.

## Acceptance evidence

- Bir rollback callback hata verse de kalan kaynaklar birer kez denenir ve original/rollback hataları korunur.
- Focused tests ve `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.

## Handoff

- V0-GOV-045

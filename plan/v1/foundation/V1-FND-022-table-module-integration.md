# V1-FND-022 - Independently verify the Tables candidate integration

- Task ID: V1-FND-022
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

dondurulmuş candidate custody hash'leriyle temiz dala alınan Tables module, project, lock ve migration setinin
eksiksizliğini ve transition invariant'ını bağımsız doğrulamak.

## Owned surface

- `database/MigrationComposition/order.json`
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`
- `src/Modules/Tables/TableLifecycle/PostgresTableRepository.cs`
- `tests/Modules/Tables/TableLifecycle/PostgresTableTests.cs`
- `evidence/V1-FND-022/**`

## In scope

- `CODE-013` için repository transition predicate'ini task-owned Table surface'te atomik kılmak.
- C52 migration SQL çiftlerini tek final `MigrationComposition` manifest integration diff'inde kaydetmek ve manifest
  testini güncellemek.

## Out of scope

- Owned surface dışındaki Tables, migration, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V0-GOV-037
- V1-FND-012
- V1-FND-021
- V1-IAM-012
- V1-CAT-003
- V1-TBL-001

## Deliverables

- Atomic Table transition + final manifest integration diff'i, task-owned tests ve raw transcript.

## Acceptance evidence

- Repository disallowed source-target transition'ı atomic predicate ile reddeder.
- Order manifesti C52 migration pair'leriyle birebir eşleşir ve manifest tests exit code `0` verir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.

## Handoff

- V0-GOV-045

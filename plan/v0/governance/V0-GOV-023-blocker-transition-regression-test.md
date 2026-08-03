# V0-GOV-023 - Test Blocker transition boundary

- Task ID: V0-GOV-023
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

`Blocked` status geçişinde yalnız eksiksiz `Blocker` bölümüne izin veren scope
kuralını otomatik regression testleriyle doğrulamak.

## Owned surface

- `tests/Architecture/TaskScope/test_task_scope_markdown_boundary.py`
- `evidence/V0-GOV-023/**`

## In scope

- `Blocked → InProgress` ve `InProgress → Blocked` legal geçişleri ile aynı
  diffte yasak görev gövdesi değişikliğinin fail-closed testleri.

## Out of scope

- Scope aracını, task standardını, application kodunu, migration'ı veya başka
  task Markdown dosyasını değiştirmek.

## Dependencies

- V0-GOV-022

## Deliverables

- Legal `Blocker` transition kabulünü ve yasak body değişikliği reddini kapsayan
  deterministic automated tests.

## Acceptance evidence

- Her iki legal status geçişi exit code `0` üretir.
- `Goal` veya `Owned surface` değişikliği aynı transition içinde non-zero exit verir.

## Handoff

- V0-GOV-018

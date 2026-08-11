# V1-SEC-004 - Independently verify migration secret redaction

- Task ID: V1-SEC-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

sentinel database password'ünün child-process argument, exception, stdout/stderr veya transcript formatting'inde görünmediğini bağımsız doğrulamak.

## Owned surface

- `src/Host/Composition/Migrations/PsqlScriptRunner.cs`
- `tests/Host/MigrationComposition/Execution/MigrationExecutionTests.cs`
- `evidence/V1-SEC-004/**`

## In scope

- `CODE-009` için Psql password formatting/exception/output redaction davranışını uygulamak ve regression testini eklemek.

## Out of scope

- Owned surface dışındaki Host composition, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V0-GOV-015
- V1-SEC-003

## Deliverables

- Migration secret-redaction implementation diff'i, sentinel regression tests ve raw transcript.

## Acceptance evidence

- Password child-process argument, exception, stdout/stderr ve transcript formatting'inde görünmez.
- Focused tests ve plan validator exit code `0` verir.

## Handoff

- V0-GOV-045

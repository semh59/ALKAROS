# V1-IAM-008 - Independently verify authorization linearization

- Task ID: V1-IAM-008
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

role/permission revoke commit'inden sonra concurrent authorization sonucunun fail-closed deny olduğunu deterministic transaction interleaving'iyle doğrulamak.

## Owned surface

- `src/Modules/Identity/Authorization/RoleManagementService.cs`
- `tests/Modules/Identity/Authorization/RoleManagementServiceTests.cs`
- `evidence/V1-IAM-008/**`

## In scope

- `CODE-008` için authorization predicate ve protected write linearization kuralını uygulamak ve formatter-only drift'i aynı owned testte kapatmak.

## Out of scope

- Owned surface dışındaki authorization, migration, project, lock veya plan dosyası değiştirmek.

## Dependencies

- V0-GOV-035
- V1-IAM-002

## Deliverables

- Authorization linearization implementation diff'i, concurrency testleri ve raw transcript.

## Acceptance evidence

- Permission decision ve mutation chosen linearization kuralına göre aynı transaction/conditional write ile bağlanır.
- Authorization tests, `dotnet format ALKAROS.slnx --verify-no-changes --no-restore` ve plan validator exit code `0` verir.

## Handoff

- V0-GOV-045

# V0-GOV-024 - Enforce Blocker transitions

- Task ID: V0-GOV-024
- Status: Done
- Assignee: /root
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

`Blocked` ile executable status arasındaki legal `Blocker` geçişlerini hem
metadata hem Markdown sınırında fail-closed doğrulamak.

## Owned surface

- `tools/task-scope/task_scope_tool.py`
- `evidence/V0-GOV-024/**`

## In scope

- Legal iki yönlü status geçişinin kabulü ve aynı diffte `Goal` veya `Owned surface`
  değişikliğinin reddi için tool ve deterministic regression testleri.

## Out of scope

- Başka task Markdown dosyasını, task standardını, application kodunu, migration'ı
  veya version gate'i değiştirmek.

## Dependencies

- V0-GOV-022

## Deliverables

- `Blocked` transition metadata kontrolü ve iki yönlü automated regression testleri.

## Acceptance evidence

- `Blocked → InProgress` ve `InProgress → Blocked` legal geçişleri exit code `0`
  üretir.
- Aynı geçişte `Goal` veya `Owned surface` değişikliği non-zero exit verir.

## Handoff

- V0-GOV-018

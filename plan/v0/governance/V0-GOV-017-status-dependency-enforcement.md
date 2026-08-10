# V0-GOV-017 - Enforce status dependency closure

- Task ID: V0-GOV-017
- Status: Done
- Assignee: /root
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Bir görevin `Done` sayılabilmesi için bütün doğrudan ve transitive task
dependency zincirinin `Done` olmasını zorunlu kılan mekanik denetimi eklemek.

## Owned surface

- `tools/plan-audit/plan_audit_tool.py`
- `plan/GATES.md`
- `plan/TASK_STANDARD.md`
- `plan/VALIDATION_CONTRACT.md`
- `plan/TRACEABILITY.md`
- `evidence/V0-GOV-017/**`

## In scope

- `Done` status dependency kapanış denetimi, V1 foundation sırası ve bu
  kuralların doğrulanabilir plan sözleşmesi.

## Out of scope

- Task statuslarını değiştirmek, product kodu, task metadata'si dışındaki
  task gövdeleri, gate kapatmak veya provider davranışı.

## Dependencies

- None

## Deliverables

- Direct/transitive status-dependency validation sonucu, tek V1 foundation
  zinciri ve yeniden üretilebilir failure/success kanıtı.

## Acceptance evidence

- Açık dependency zincirine sahip `Done` görev `DONE_DEPENDENCY_NOT_FINAL`
  hatası üretir.
- Açık ancestor'a sahip `Done` görev `DONE_DEPENDENCY_TRANSITIVE_NOT_FINAL`
  hatası üretir.

## Handoff

- V0-GOV-018

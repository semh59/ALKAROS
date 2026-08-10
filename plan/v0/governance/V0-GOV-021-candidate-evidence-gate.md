# V0-GOV-021 - Enforce candidate evidence gate

- Task ID: V0-GOV-021
- Status: Done
- Assignee: /root
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Mevcut application ağacını candidate evidence olarak korurken V0 açıkken yeni
application görevinin `InProgress` durumuna geçmesini fail-closed reddetmek.

## Owned surface

- `tools/plan-audit/plan_audit_tool.py`
- `plan/GATES.md`
- `plan/VALIDATION_CONTRACT.md`
- `plan/TRACEABILITY.md`
- `evidence/V0-GOV-021/**`

## In scope

- Candidate evidence ayrımı, V0 açıkken application status kontrolü ve
  doğrulanabilir validator çıktısı.

## Out of scope

- Task statuslarını değiştirmek, product kodu, test, migration, Git geçmişi
  veya V0 gate kapanışı.

## Dependencies

- V0-GOV-017

## Deliverables

- Candidate evidence ağacını kabul eden ve erken application `InProgress`
  durumunu reddeden fail-closed plan validation kuralı.

## Acceptance evidence

- Mevcut Git ve application ağacı tek başına validation hatası üretmez.
- V0 altında açık `Blocked` task varken application task `InProgress` ise
  `APPLICATION_STARTED_BEFORE_V0_EXIT` hatası üretilir.

## Handoff

- V0-GOV-018

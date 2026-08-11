# V0-GOV-054 - Recognize the C54 application admission exactly

- Task ID: V0-GOV-054
- Status: InProgress
- Assignee: /root/implement_v0_gov_054
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52
- CORR:C53
- CORR:C54
- CORR:C56
- CORR:C57

## Goal

Plan semantic validation'ın, C54 ile tanınan yalnız `V1-FND-023` remediation
uygulamasını exact source/date/19-ID tuple/dependency koşulları altında
başlatabilmesini sağlamak; diğer V1 application veya old `Done` task için
exception üretmemek.

## Owned surface

- `tools/plan-audit/plan_audit_tool.py`
- `tests/Architecture/PlanAudit/test_plan_audit.py`
- `plan/VALIDATION_CONTRACT.md`
- `evidence/V0-GOV-054/**`

## In scope

- `V1-FND-023` için C54 one-time `Directory.Build.targets` authority, source
  basis, approval date, 19-ID admission tuple ve Done dependency koşullarını
  birlikte fail-closed doğrulamak.
- Bu koşullar geçerliyse yalnız `V1-FND-023` `InProgress` durumunda
  `APPLICATION_STARTED_BEFORE_V0_EXIT` üretmemek.
- Extra/old Done/başka V1 ID, missing veya malformed C54 authority, wrong
  source/date/tuple ve açık dependency vakalarını negatif fixture ile reddetmek.

## Out of scope

- `V1-FND-023` target/test implementationını, GATES veya task-scope admission
  table'ını değiştirmek.
- Başka bir V1 taska C54 veya application-start exception tanımak.

## Dependencies

- V0-GOV-046
- V0-GOV-050

## Deliverables

- C54-only semantic admission verifier, negative regression matrix ve güncel
  contract açıklaması.

## Acceptance evidence

- Exact valid C54 `V1-FND-023` fixture'ı plan validation exit `0` verir.
- Her wrong/extra/old-Done/missing dependency authority tuple fixture'ı
  deterministic non-zero semantic error verir.
- PlanAudit tests, plan validation, pre-Done task-scope ve diff check exit `0`
  verir; evidence yalnız `evidence/V0-GOV-054/**` altındadır.

## Handoff

- V1-FND-023
- V0-GOV-045
- V0-GOV-048

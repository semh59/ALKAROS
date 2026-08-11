# V0-GOV-059 - Align V3 reentry parent with latest admission hardening

- Task ID: V0-GOV-059
- Status: InProgress
- Assignee: /root/implement_v0_gov_059
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C62

## Goal

`V1-FND-023` kesintiye uğramış kapanış denetimini, eski `V0-GOV-055` finali
yerine güncel admission-hardening finalini içeren yeni governance finaline
bağlamak; böylece `V0-GOV-057` düzeltmesi zincirin dışında kalmadan yeni
reentry/evidence/final kapanışı kurulabilsin.

## Owned surface

- `tools/evidence-envelope/evidence_envelope_tool.py`
- `tests/Architecture/EvidenceEnvelope/test_evidence_envelope.py`
- `tools/plan-audit/plan_audit_tool.py`
- `tests/Architecture/PlanAudit/test_plan_audit.py`
- `docs/engineering/closure-evidence-envelope.md`
- `plan/VALIDATION_CONTRACT.md`
- `evidence/V0-GOV-059/**`

## In scope

- Fixed v3 validator'ın reentry parent kontrolünü, `V0-GOV-055` finaline
  kilitlemek yerine güncel admission-hardening finalinin direct child'ı olacak
  şekilde fail-closed hizalamak.
- Eski `V0-GOV-055` parentli closure'un ve unrelated valid v2 finalin
  `V1-FND-023` `Done` admission'i yerine gecmesini deterministic reddetmek.
- `V1-FND-023` yeni reentry commit'i icin parent task, ancestry, source blob,
  blocker removal, evidence checkpoint, final metadata ve trailer kontrollerini
  regression testleriyle korumak.
- Closure contract ve PlanAudit entegrasyonunu yeni parent semantigiyle ayni
  hale getirmek.

## Out of scope

- `V1-FND-023` source/test-discovery davranisini, evidence'ini veya statusunu
  kapatmak.
- `V0-GOV-055`, `V0-GOV-057` veya baska historical `Done` task'in status,
  assignee, acceptance/evidence ya da commit tarihcesini degistirmek.
- Universal v3 closure mekanizmasi veya baska task icin yeni exception uretmek.

## Dependencies

- V0-GOV-057

## Deliverables

- Guncel admission-hardening finaline bagli task-specific v3 verifier, PlanAudit
  entegrasyonu, negatif regression matrisi ve hashli raw acceptance kaniti.

## Acceptance evidence

- `V1-FND-023` `Done` admission'i yalniz yeni governance finalinin direct
  child'i olan reentry uzerinden kurulan fixed B0/interruption/A/E/F zinciriyle
  kabul edilir; eski `V0-GOV-055` parentli veya generic v2 final deterministic
  non-zero verir.
- EvidenceEnvelope ve PlanAudit testleri, plan validation, pre-Done task-scope
  ve diff check exit code `0` verir; raw transcriptler yalniz
  `evidence/V0-GOV-059/**` altindadir.

## Handoff

- V1-FND-023
- V0-GOV-045
- V0-GOV-048

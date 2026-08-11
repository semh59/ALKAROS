# V0-GOV-060 - Align V3 B0 source blob constants

- Task ID: V0-GOV-060
- Status: InProgress
- Assignee: /root
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C63

## Goal

`V1-FND-023` v3 verifier sabitlerini gerçek immutable B0 source bloblarıyla
hizalamak ve yeni closure denemesini bu task'ın final governance commit'inin
direct child reentry zincirine bağlamak.

## Owned surface

- `tools/evidence-envelope/evidence_envelope_tool.py`
- `tests/Architecture/EvidenceEnvelope/test_evidence_envelope.py`
- `tools/plan-audit/plan_audit_tool.py`
- `tests/Architecture/PlanAudit/test_plan_audit.py`
- `docs/engineering/closure-evidence-envelope.md`
- `plan/VALIDATION_CONTRACT.md`
- `evidence/V0-GOV-060/**`

## In scope

- `_V3_SOURCE_ARTIFACTS` sabitlerini `fd3344f15c5257b53bf5281ee9129f800c62f0a7`
  commitindeki gerçek `Directory.Build.targets` ve
  `tests/Architecture/TestDiscovery/test_solution_test_discovery.py` SHA-256
  değerleriyle eşlemek.
- V3 reentry parent task sabitini bu task'ın finaliyle hizalamak ve eski
  `V0-GOV-059` parentli finalin yeni `V1-FND-023` admission yerine geçmesini
  deterministic reddetmek.
- B0 blob hash mismatch ve source-artifact envelope mismatch durumlarını
  regression testleriyle fail-closed korumak.
- Closure contract ve PlanAudit entegrasyonunu aynı semantiğe taşımak.

## Out of scope

- `V1-FND-023` source/test-discovery davranışını, evidence'ini veya statusunu
  kapatmak.
- `V0-GOV-059` veya başka historical `Done` task'ın status, assignee,
  acceptance/evidence ya da commit tarihçesini değiştirmek.
- Universal v3 closure mekanizması veya başka task için yeni exception üretmek.

## Dependencies

- V0-GOV-059

## Deliverables

- Gerçek B0 blob hashleriyle hizalanmış task-specific v3 verifier, PlanAudit
  guard'ı, negatif regression matrisi ve hashli raw acceptance kanıtı.

## Acceptance evidence

- V3 verifier gerçek B0 source bloblarını kabul eder; eski yanlış hash,
  eski `V0-GOV-059` parentli final ve source-artifact mismatch deterministic
  non-zero verir.
- EvidenceEnvelope ve PlanAudit testleri, plan validation, pre-Done task-scope
  ve diff check exit code `0` verir; raw transcriptler yalnız
  `evidence/V0-GOV-060/**` altındadır.

## Handoff

- V1-FND-023
- V0-GOV-045
- V0-GOV-048

# V0-GOV-057 - Harden V3 admission for the interrupted FND-023 closure

- Task ID: V0-GOV-057
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C60

## Goal

`V1-FND-023` `Done` admissionını V0 gate durumundan bağımsız olarak yalnız fixed
v3 closure zincirine bağlamak; generic valid v2 `HEAD` sonucunun v3 kabulü
yerine geçmesini fail-closed engellemek.

## Owned surface

- `tools/evidence-envelope/evidence_envelope_tool.py`
- `tests/Architecture/EvidenceEnvelope/test_evidence_envelope.py`
- `tools/plan-audit/plan_audit_tool.py`
- `tests/Architecture/PlanAudit/test_plan_audit.py`
- `docs/engineering/closure-evidence-envelope.md`
- `plan/VALIDATION_CONTRACT.md`
- `evidence/V0-GOV-057/**`

## In scope

- `V1-FND-023` `Done` kontrolünü V0 gate açık veya kapalı olsa da çalıştırmak.
- PlanAudit'in task-specific v3 verifier çağrısını yalnız `V1-FND-023` için
  fixed B0, interruption ve `B0 → interruption → A → E → F` topolojisine
  bağlamak; generic valid v2 `HEAD` sonucunu reddetmek.
- B0 hash/artifactları, ancestry/interruption, blocker diff'i, A/E/F path
  diff'leri, raw/evidence envelope, tampered hash, worktree substitution,
  extra/misordered trailer, other task ve V0 gate açık/kapalı vakalarının her
  birini deterministic negatif regression ile kapsamak.
- V3 verifier'ın missing veya invalid task-specific closure sonucunu
  `V1-FND-023` `Done` admissionı için non-zero hata olarak raporlamak.

## Out of scope

- `V0-GOV-055` veya başka historical `Done` task'ın status, assignee,
  acceptance/evidence ya da commit tarihçesini değiştirmek veya yeniden açmak.
- Başka task, subject, interruption veya arbitrary v2/v3 closure zinciri için
  universal exception üretmek; `V1-FND-023` product/test-discovery davranışını
  değiştirmek.

## Dependencies

- V0-GOV-055

## Deliverables

- `V1-FND-023`e bağlı fixed v3 admission verifier, geniş negatif regression
  matrisi, güncel closure contractı ve hashli raw acceptance kanıtı.

## Acceptance evidence

- `V1-FND-023` `Done` kontrolü V0 gate hem açıkken hem kapalıyken çalışır ve
  yalnız task-specific v3 verifier geçerse kabul edilir; generic valid v2 `HEAD`
  deterministic non-zero ile reddedilir.
- Fixed B0 hash/artifact, ancestry/interruption, exact blocker diff'i, A/E/F
  path diff'leri, missing raw/evidence envelope, tampered hash, worktree
  substitution, extra veya misordered trailer ve other-task vakalarının her biri
  ayrı negatif testte deterministic non-zero verir.
- EvidenceEnvelope ve PlanAudit testleri, plan validation, pre-Done task-scope
  ve diff check exit code `0` verir; raw transcriptler yalnız
  `evidence/V0-GOV-057/**` altındadır.

## Handoff

- V1-FND-023
- V0-GOV-045
- V0-GOV-048

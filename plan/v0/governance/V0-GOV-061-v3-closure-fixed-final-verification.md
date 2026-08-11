# V0-GOV-061 - Verify V1-FND-023 v3 closure against the fixed final commit

- Task ID: V0-GOV-061
- Status: Done
- Assignee: /root/implement_v0_gov_061
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C64

## Goal

`V1-FND-023` v3 interrupted closure admission'ının, current `HEAD` referansı
yerine closure'ın kendisinde kayıtlı fixed final commit (`53bde4988f336e9481d57bce3319e6a658d44a2d`)
üzerinden doğrulanmasını sağlamak; böylece closure tamamlandıktan sonra atılan
her yeni görev commit'i (`validate` başarısını bozmadan) geçerli kalmalı, ancak
closure'a dokunan her sapma deterministic fail-closed reddedilmelidir.

## Owned surface

- `evidence/V0-GOV-061/**`

CORR:C64, aşağıdaki exact yolların ileri custody'sini V0-GOV-061 devralır:
tools/evidence-envelope/evidence_envelope_tool.py, tests/Architecture/EvidenceEnvelope/test_evidence_envelope.py,
tools/plan-audit/plan_audit_tool.py, tests/Architecture/PlanAudit/test_plan_audit.py,
docs/engineering/closure-evidence-envelope.md ve plan/VALIDATION_CONTRACT.md.
Bu historical `V0-GOV-060` closed kalır.

## In scope

- `evidence_envelope_tool.py` içine v3 zincir sabitlerinin mevcut deseniyle
  uyumlu `_V3_FINAL_COMMIT = "53bde4988f336e9481d57bce3319e6a658d44a2d"`
  sabitini eklemek; sabit eksik/bölünmüş/sahte ise reddeden bir
  `resolve_v3_final_commit` fail-closed sözleşmesi tanımlamak.
- `plan_audit_tool.py` `v3_interrupted_closure_errors` içinde `git rev-parse
  HEAD` ile geçen generic "current HEAD" commit'ini kaldırmak; bunun yerine
  closure doğrulamasını `_V3_FINAL_COMMIT` sabiti üzerinden yürütmek ve sabit
  final'ın repository'de mevcut olduğunu, `HEAD`'in sabit final'ın descendant'ı
  (veya bizzat kendisi) olduğunu fail-closed doğrulamak.
- Sabit final'ın repository'de bulunmaması, malformed sabit, `HEAD`'in sabit
  final'ın ancestor'ı olması/bağlantısız olması, v3 zincirine herhangi bir
  müdahale veya generic v2-commit kabulü durumlarında deterministic non-zero
  regression testleri eklemek; closure-valid iken yeni commit'in hata üretmediği
  mevcut senaryoları korumak.
- `plan/VALIDATION_CONTRACT.md` v3 doğrulama cümlesini "current HEAD exact v3 F
  final" yerine "fixed v3 F final + HEAD descendant" semantiğine güncellemek.
- `_V3_FINAL_COMMIT` sabitinin gerçek test/repo'da `validate_v1_fnd_023_v3_final_commit`
  ile `valid` ürettiğini ve repository genelinde `plan_audit_tool.py validate`
  exit code `0` olduğunu kanıtlamak.

## Out of scope

- `V1-FND-023` B0/interruption sabitlerini, status/assignee, acceptance/evidence
  veya commit tarihçesini değiştirmek; `V1-FND-023` product/test-discovery
  davranışına dokunmak.
- `V0-GOV-060` historical closure'ını yeniden açmak ya da onun status, assignee,
  acceptance/evidence veya commit tarihçesini değiştirmek.
- Universal v3 mekanizması veya başka task için exception üretmek; TaskScope
  tool/parity invariant yüzeyine, plan/GATES/TRACEABILITY dokümanlarına
  (task dosyası hariç) veya başka görev dosyasına dokunmak.

## Dependencies

- V0-GOV-060

## Deliverables

- Fixed v3 final commit'e bağlı verifier sabiti ve fail-closed resolver'ı;
  PlanAudit closure guard'ının HEAD-bağımsız sözleşmesi; contract metni
  güncellemesi; negatif/pozitif regression matrisi; V061'e ait hashli raw
  acceptance kanıtı.

## Acceptance evidence

- `evidence_envelope_tool.py` v3 zincir testleri ve `plan_audit_tool.py`
  closure testleri geçer; sabit final / descendant HEAD kombinasyonlarında
  `valid`/exit `0`, ancestor veya bağlantısız HEAD, malformed/missing sabit,
  repository'de bulunmayan final ve zincire müdahale senaryolarında
  deterministic non-zero red üretir.
- `py -B tools/plan-audit/plan_audit_tool.py validate` gerçek repository
  HEAD'inde exit code `0` verir ve `C54_APPLICATION_ADMISSION_V3_CLOSURE_INVALID`
  / `C54_APPLICATION_ADMISSION_V3_FINAL_MISSING` üretmez.
- `py -B tools/evidence-envelope/evidence_envelope_tool.py --final-commit
  53bde4988f336e9481d57bce3319e6a658d44a2d --repository . --format json`
  `"valid": true` üretir.
- `py -m pytest tests/Architecture/EvidenceEnvelope tests/Architecture/PlanAudit -q`
  ve ilgili suite exit code `0` verir; raw transcriptler yalnız
  `evidence/V0-GOV-061/**` altındadır.

## Handoff

- V0-GOV-058
- V0-GOV-045
- V0-GOV-048
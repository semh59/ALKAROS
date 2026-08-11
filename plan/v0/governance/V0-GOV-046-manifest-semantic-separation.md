# V0-GOV-046 - Separate byte integrity from governance semantics

- Task ID: V0-GOV-046
- Status: InProgress
- Assignee: /root/implement_v0_gov_046
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52
- CORR:C55
- CORR:C56

## Goal

Plan audit manifest byte/hash doğrulaması ile task/gate/evidence semantic kontrollerini ayrı ve fail-closed kapılar haline getirmek.

## Owned surface

- `tools/plan-audit/plan_audit_tool.py`
- `tests/Architecture/PlanAudit/test_plan_audit.py`
- `plan/VALIDATION_CONTRACT.md`
- `evidence/V0-GOV-046/**`

## In scope

- Byte manifest geçerken stale status, revalidation conflict, final Done write-set ve non-Markdown evidence tampering fixture'larını semantic kapıda reddetmek.
- Her kapının ayrı command, exit code ve error taxonomy üretmesini sağlamak.
- Evidence envelope alanlarını `V0-GOV-039` sözleşmesinden doğrulamak.
- `VALIDATION_CONTRACT`, `GATES.md` ve task-scope tool admission tuple'ı
  ayrıştığında semantic validation'ın fail-closed dönmesini sağlamak.
- PDF coverage ölçümünü yalnız immutable historical trace girdisi olarak
  etiketlemek; current remediation source authority'sini Markdown source
  register ve `CORR:C52` zincirinden doğrulamak.

## Out of scope

- Manifest hash kontrolünü gevşetmek veya semantic failure'ı warning'e çevirmek.
- Task/gate kaydını bu validator görevi içinde düzeltmek.

## Dependencies

- V0-GOV-035
- V0-GOV-052

## Deliverables

- Ayrı byte ve semantic validation commands, negative fixtures ve contract update.

## Acceptance evidence

- Byte-valid/semantic-invalid bütün fixtures non-zero semantic sonuç verir.
- Non-Markdown evidence tampering hash/verdict mismatch ile yakalanır.
- PDF-only coverage sonucu current remediation source basis veya acceptance
  kanıtını tek başına kapatamaz; C52 tasklerinin effective source kaydı
  `CORR:C52` olarak okunur.
- Contract/GATES/tool exact 19-ID admission tuple divergence'ı non-zero
  semantic validation ile reddedilir.
- Plan audit testleri ve repository semantic validation exit code `0` verir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V0-GOV-046/**` altındadır.

## Handoff

- V0-GOV-041
- V0-GOV-045

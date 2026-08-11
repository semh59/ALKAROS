# V0-GOV-055 - Close the interrupted V1-FND-023 remediation

- Task ID: V0-GOV-055
- Status: InProgress
- Assignee: /root/implement_v0_gov_055
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C58
- CORR:C59

## Goal

`V1-FND-023` için kesintiye uğramış immutable remediation zincirini, yalnız
doğrulanmış B0 ve interruption commitleri üzerinden v3 fail-closed closure
verifier ile yeniden kapatılabilir yapmak.

## Owned surface

- `tools/evidence-envelope/evidence_envelope_tool.py`
- `tests/Architecture/EvidenceEnvelope/test_evidence_envelope.py`
- `docs/engineering/closure-evidence-envelope.md`
- `tools/plan-audit/plan_audit_tool.py`
- `tests/Architecture/PlanAudit/test_plan_audit.py`
- `plan/VALIDATION_CONTRACT.md`
- `evidence/V0-GOV-055/**`

## In scope

- V3'ü yalnız `V1-FND-023` için sabit B0
  `fd3344f15c5257b53bf5281ee9129f800c62f0a7` ve interruption
  `479881636c8142c7161f2d5980d37ca2f9b48591` ile sınırlamak.
- B0'ın parent'ını, üç değişen yolunu ve blob bytes'ını; interruption'ın B0
  doğrudan çocuğu olduğunu ve yalnız exact `InProgress`→`Blocked` metadata ile
  exact `Blocker` diff'ini byte/diff/topology olarak doğrulamak.
- Reentry A'nın interruption'ın doğrudan çocuğu olduğunu, yalnız exact
  `Blocked`→`InProgress` geçişi ile aynı `Blocker` bölümünü kaldırdığını; E'nin
  A'nın doğrudan çocuğu olarak yalnız `evidence/V1-FND-023/**` eklediğini ve
  F'nin E'nin doğrudan çocuğu olarak yalnız `InProgress`→`Done` status satırını
  değiştirdiğini doğrulamak.
- B0 subject artifact bloblarının A/E/F'de stale veya değiştirilmiş olmamasını,
  E tree'sindeki envelope/raw bytes'ını, v3 trailer bloğunu ve worktree
  substitution korumasını birlikte fail-closed doğrulamak.
- Yanlış task, subject, interruption, parent/topology, byte/diff, reentry,
  evidence, final metadata veya trailer vakasını deterministik reddeden negatif
  regression testleri eklemek.

## Out of scope

- Başka task, başka interruption veya arbitrary B→E→F zinciri için istisna
  üretmek; `V0-GOV-052` veya `V0-GOV-054` historical closure'ını yeniden açmak.
- `V1-FND-023` target/test davranışını, immutable B0/interruption commitlerini
  veya geçmişi değiştirmek.

## Dependencies

- V0-GOV-052
- V0-GOV-054
- V0-GOV-056

## Deliverables

- V1-FND-023'e özgü v3 verifier, byte/diff/topology negative matrix, güncel
  closure sözleşmesi, semantic plan gate ve hashli raw acceptance kanıtı.

## Acceptance evidence

- Yalnız `B0 → interruption → A → E → F` topolojisi, fixed B0/interruption
  SHA'ları, exact path/blob/diff kuralları ve v3 trailerları ile zero exit verir.
- Altered B0/interruption bytes, alternate task/interruption, non-adjacent A/E/F,
  stale subject blob, evidence dışı E diff, worktree substitution ve final
  metadata/trailer sapmaları deterministic non-zero verir.
- EvidenceEnvelope ve PlanAudit testleri, plan validation, pre-Done task-scope
  ve diff check exit code `0` verir; raw transcriptler yalnız
  `evidence/V0-GOV-055/**` altındadır.
- Plan-audit `validate` exit code `0`, C59 routing/catalog parity'sinin bağımsız
  kabul kanıtı değildir; bu parity yalnız `V0-GOV-056` TaskScope evidence'ı ile
  doğrulanır.

## Handoff

- V1-FND-023
- V0-GOV-045
- V0-GOV-048

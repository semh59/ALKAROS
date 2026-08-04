# V0-GOV-031 - Approve C42 remediation entry-gate exceptions

- Task ID: V0-GOV-031
- Status: Done
- Assignee: opencode-v0-gov-031
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C43

## Goal

C42 kullanıcı onaylı remediasyon görevlerinin (`V1-IAM-005`, `V1-FND-013`,
`V1-FND-014`, `V1-FND-015`) `GATE-V0-EXIT` türetilmiş kontrolü açıkken
(11 devir + 1 planned V0 görevi) `InProgress` olabilmesi için task-scope
aracının onay setini ve GATES.md remediasyon istisna tablosunu aynı kimliklerle
güncellemek.

## Owned surface

- `tools/task-scope/task_scope_tool.py`
- `tests/Architecture/TaskScope/test_task_scope.py`
- `docs/engineering/task-scope-contract.md`
- `plan/GATES.md`
- `plan/VALIDATION_CONTRACT.md`
- `evidence/V0-GOV-031/**`

## In scope

- `_APPROVED_REMEDIATION_TASK_IDS` sabit kümesine `V1-IAM-005`,
  `V1-FND-013`, `V1-FND-014`, `V1-FND-015` kimliklerini eklemek.
- `_REMEDIATION_EXCEPTION_ROW` approval tarihi ifadesine `2026-08-04` eklemek;
  aksi halde C43 satırları fail-closed reddedilir.
- `plan/GATES.md` `TASK_SCOPE_REMEDIATION_EXCEPTIONS` tablosuna aynı dört
  kimliği `2026-08-04` kayıtlarıyla yazmak; tablo ile araç kodu birebir
  eşleşir.
- `tests/Architecture/TaskScope/test_task_scope.py` `REMEDIATION_ROWS`
  fixture'ını dört `2026-08-04` satırıyla genişletmek ve fail-closed
  testlerinin geçtiğini kanıtlamak.
- `docs/engineering/task-scope-contract.md` ve `plan/VALIDATION_CONTRACT.md`
  içindeki onay kümesi tarih ifadelerini `2026-08-04`'ü kapsayacak şekilde
  güncellemek.

## Out of scope

- V0 task durumunu kapatmak, yeni ürün davranışı eklemek, gate kapanış kanıtı
  üretmek veya `_CANDIDATE_CODE_REMEDIATION_TASK_IDS` kümesini değiştirmek.

## Dependencies

- V0-GOV-028

## Deliverables

- Dört C42 kimliği için entry-gate istisnası: araç onay seti + GATES.md
  tablo kayıtları + sözleşme metinleri + fail-closed test kanıtı.

## Acceptance evidence

- `py -m pytest tests/Architecture/TaskScope -q` exit code `0`.
- `py tools/task-scope/task_scope_tool.py --task-id V1-IAM-005 --format text`
  ve diğer üç C42 kimliği için gate istisnası kabulü.
- `py tools/plan-audit/plan_audit_tool.py validate` exit code `0`.
- Komut, exit code ve sonuç `evidence/V0-GOV-031/**` altında kayıtlıdır.

## Handoff

- V1-IAM-005
- V1-FND-013
- V1-FND-014
- V1-FND-015

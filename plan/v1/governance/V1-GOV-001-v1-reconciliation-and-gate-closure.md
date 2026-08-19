# V1-GOV-001 - V1 reconciliation, audit report regen and gate closure

- Task ID: V1-GOV-001
- Status: InProgress
- Assignee: Antigravity-v1-gov-001
- Work type: validation
- Surface state: Existing

## Goal

V1 remediasyon görevlerinin tamamlanması ardından audit raporunu ve manifestini temiz çalışma alanında yeniden üretmek,
`verify-manifest` sıfır hata doğrulaması yapmak ve `GATE-V1-EXIT` resmi kapanış kaydını üretmek.

## Owned surface

- `plan/v1/README.md`
- `plan/GATES.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/v1/gate-v1-exit-closure.md`

## Dependencies

- V1-BIL-004
- V1-FND-025
- V1-TBL-007
- V1-ORD-004
- V1-IAM-015
- V1-WTR-004

## Acceptance evidence

- `python -B tools/plan-audit/plan_audit_tool.py validate` exit 0 verir.
- `python -B tools/plan-audit/plan_audit_tool.py verify-manifest` 0 hata ile exit 0 verir.
- `evidence/v1/gate-v1-exit-closure.md` altında formal kapanış kanıtı kaydedilir.
- `task_scope_tool.py --task-id V1-GOV-001` exit 0 verir.

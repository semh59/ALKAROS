# V1-GOV-002 - Post-audit V1 gate reseal

- Task ID: V1-GOV-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Existing

## Goal

`V1-WTR-005` bağımsız denetim remediasyonu tamamlandıktan sonra V1 sayımını, audit raporunu, manifesti ve resmî
gate kapanış kanıtını temiz çalışma alanında yeniden mühürlemek.

## Owned surface

- `plan/v1/README.md`
- `plan/GATES.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/v1/gate-v1-exit-closure.md`

## Dependencies

- V1-GOV-001
- V1-WTR-005

## Acceptance evidence

- `python -B tools/plan-audit/plan_audit_tool.py validate` exit 0 verir.
- `python -B tools/plan-audit/plan_audit_tool.py validate-coverage` exit 0 verir.
- `python -B tools/plan-audit/plan_audit_tool.py verify-manifest` sıfır hata ile exit 0 verir.
- `python -B tools/task-scope/task_scope_tool.py --task-id V1-GOV-002 --format text` exit 0 verir.
- Semih, temiz checkout'ta `verify-manifest` komutuyla aynı manifest hash'ini yeniden üretebilir.

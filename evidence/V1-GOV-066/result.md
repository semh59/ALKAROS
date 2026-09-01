# V1-GOV-066 - Wave 22 reopen result

- Date: 2026-09-01

`GATE-V1-EXIT` reopened for remediation wave 22 (WAL archiving / point-in-time
recovery + competitively calibrated RPO/RTO targets), per Semih approval
2026-09-01.

- `plan/GATES.md` line 36 + reopen narrative updated; V1 matrix → 241 tasks.
- `plan/v1/README.md` matrix and wave list updated.
- Surface handover: `docs/recovery/rpo-rto-targets.md` from `V0-BKP-002` to
  `V1-RMD-095`; `tests/Architecture/TaskScope/test_task_scope.py` from
  `V0-GOV-063` to `V1-GOV-066`.
- `V0-BKP-001` / `V0-BKP-002` closed Done with `## Onay` blocks; removed from the
  `V0_DEFERRED_TASKS` set in `plan/GATES.md`, `tools/plan-audit/plan_audit_tool.py`,
  `tools/task-scope/task_scope_tool.py` and
  `tests/Architecture/TaskScope/test_task_scope.py` (11 → 9 deferred tasks).
- `plan/TRACEABILITY.md` C73 recorded.
- `plan/OFFICIAL_SOURCE_REGISTER.md`: `V1-RMD-095` added to the POSTGRESQL-18.4
  consumer list (EXT ref in the task).

Checks: `plan_audit_tool.py validate` 0 errors / 0 warnings (551 md, 529 task
files); `consistency_audit.py` clean; `pytest tests/Architecture/TaskScope
tests/Architecture/PlanAudit` 162 passed.

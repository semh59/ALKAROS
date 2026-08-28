# V1-GOV-005 validation results

## Final commands

- `python -B tools/plan-audit/plan_audit_tool.py validate`: exit `0`; 400 Markdown files, 378 task files,
  18 gates, 1.321 dependency edges, 0 errors, 0 warnings.
- Audit findings JSON and 1.727 ledger JSONL rows parse: exit `0`.
- Ledger verdict distribution after custody correction: 1.150 `REJECTED_UNVERIFIED`, 552 `INFORMATIONAL`,
  15 `BLOCKED_EXTERNAL`, 10 `FAIL_FINDING`, 0 `PASS`.
- `python -B tools/task-scope/task_scope_tool.py --task-id V1-GOV-005`: exit `1`; initial mixed dirty write-set and
  untracked earlier evidence are outside this task's allowlist. This is not waived.
- `git diff --check`: exit `2`; remaining failure is pre-existing production path
  `src/Host/DualScreen/DualScreenStore.cs:262` trailing whitespace. This task did not modify production code.
- Independent fresh PostgreSQL full-solution run: exit `1` because Host tests could not locate `psql` executable.
- `dotnet format --verify-no-changes`: exit `2`.

## Custody verdict

The prior full-audit/remediation reseal is rejected. V1-GOV-003 and the remediation chain require fresh single-agent
execution. V1-GOV-005 remains Blocked because task-scope/diff gates are non-zero and WebPrototype custody cannot be
transferred without owning the historical V1-RMD-004 task file.

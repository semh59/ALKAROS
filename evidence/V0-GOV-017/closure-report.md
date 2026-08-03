# V0-GOV-017 Closure Report

- Date: 2026-08-03
- Task: `V0-GOV-017`
- Result: `Done`

## Commands

```text
py tools/task-scope/task_scope_tool.py --task-id V0-GOV-017 --repo-root D:\PROJECT\ALKAROS --format json
Exit code: 0
valid: true

py -m py_compile tools\plan-audit\plan_audit_tool.py
Exit code: 0

py tools\plan-audit\plan_audit_tool.py validate
Exit code: 1 (expected before V0-GOV-018)
DONE_DEPENDENCY_NOT_FINAL: 32
DONE_DEPENDENCY_TRANSITIVE_NOT_FINAL: 1123
```

## Result

The validator now rejects every `Done` task with a non-final direct dependency
or a non-final transitive ancestor. V0-GOV-018 owns the status reconciliation
required for the command to return zero errors.

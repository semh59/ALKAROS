# V0-GOV-034 checkpoint before ownership transfer

- Task ID: `V0-GOV-034`
- Checkpoint state: `InProgress`; this is not closure evidence.
- Date: 2026-08-10
- Baseline commit: `ceda19c3bdd5ca0333eebf01e11cec0f4ce30c6b`

## Reason for the checkpoint

Independent governance review reproduced a source-authority conflict: C52 in
`plan/TRACEABILITY.md` prohibits reopening existing `Done` tasks, whereas an
earlier draft would have admitted 18 existing custodians as remediation
candidates. The draft was rejected. No existing `Done` task has been changed
or admitted as a remediation candidate.

`V0-GOV-037` is materialized as the separate, single-purpose transfer task.
It must move exact writable-surface custody from the existing historical task
records to new C52 remediation tasks without changing the old tasks' status or
assignee. This task remains `InProgress` until that independent transfer is
complete and audited.

## Reproduced controls

| Check | Result |
| --- | --- |
| `python -B tools/plan-audit/plan_audit_tool.py validate` | exit code `0`; 0 errors; 0 warnings |
| `python -B tools/task-scope/task_scope_tool.py --task-id V0-GOV-034 --format text` | exit code `0`; all current paths within scope |
| CSV/JSON routing parity | exit code `0`; exact finding/owner/prerequisite/route parity |
| Routing owner status check | exit code `0`; no owner is an existing `Done` task |
| Embedded task catalog/document dependency parity | exit code `0` |
| New task source-basis check | exit code `0`; all 67 materialized C52 tasks reference `CORR:C52` |
| `git diff --check` | exit code `0` |

## Known non-closure control

Mandatory Markdown lint is not asserted green here. Its established timeout or
failure remains a downstream control gap owned by `V0-GOV-044`; this checkpoint
does not reinterpret it as passing.

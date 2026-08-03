# V0-GOV-001 verification

## Executed checks

- `py -m py_compile tools/task-scope/task_scope_tool.py` exited `0`.
- `py -m pytest tests/Architecture/TaskScope/test_task_scope_markdown_boundary.py -q`
  exited `0`: `7 passed`.
- `git diff --check -- tools/task-scope/task_scope_tool.py
  tests/Architecture/TaskScope/test_task_scope_markdown_boundary.py
  docs/engineering/task-scope-contract.md` exited `0`.

## Covered rejection cases

- A committed task Markdown that adds an `Owned surface` entry is rejected, and
  the newly named external path remains outside the baseline allowlist.
- Only `Status` and `Assignee` changes, an existing owned path and the task's
  own evidence directory are accepted for a committed, gate-open task.
- `Done`, `Blocked` and `NotApplicable` tasks are non-executable.
- An open preceding release version keeps its entry gate open.
- An untracked task Markdown cannot define a write allowlist.

## Legacy test conflict

`py -m pytest tests/Architecture/TaskScope -q` exited `1`: `23 failed,
39 passed`. The 23 failures are legacy fixtures that create the task Markdown
after the Git baseline and omit the preceding-version gate records; one also
asserts that `Planned` must be rejected. Those expectations conflict with this
task's fail-closed requirements. The failing files are outside this task's
Owned surface and were not altered.

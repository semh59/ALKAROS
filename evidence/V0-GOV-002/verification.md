# V0-GOV-002 verification

- Command: `py -m pytest tests/Architecture/TaskScope -q`
- Exit code: `0`
- Result: `62 passed in 42.55s`

The fixture now commits a V0 prerequisite and the active task Markdown before
creating only a permitted `Assignee` metadata change. It therefore exercises
the same committed-baseline and preceding-gate preconditions as the strict
scope tool instead of treating an untracked task Markdown as trusted input.

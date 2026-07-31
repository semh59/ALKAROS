# V1-FND-003 Test Results

Date: 2026-07-31
Assignee: cline-v1-fnd-003

## Command

`python -m pytest tests/Architecture/TaskScope -q`

## Result

- Exit code: 0
- Passed: 39
- Failed: 0
- Skipped: 0
- Duration: 18.37s

## Coverage

- allow/deny path matching
- dirty-worktree detection
- untracked paths
- deleted paths
- rename (old/new path) enforcement
- path traversal rejection
- Windows path case/separator normalization
- metadata fail-closed cases (missing/multiple Task ID, invalid status/assignee,
  incomplete dependency, broken Markdown)
- local/CI identical result contract on the same fixture set

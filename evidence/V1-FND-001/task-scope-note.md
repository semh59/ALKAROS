# Task-scope interpretation note (V1-FND-001)

- Date: 2026-07-31
- Executor: opencode-v1-fnd-001

## Tool result

`python tools/task-scope/task_scope_tool.py --task-id V1-FND-001` returned `"valid": false`
with 25 findings. ALL 25 findings point to files written by the previous session
(V1-FND-004) that are still uncommitted in the working tree:
`plan/v1/foundation/V1-FND-004-*.md`, `database/MigrationComposition/**`,
`src/Host/Composition/**`, `src/Host/Program.cs`, `tests/Host/MigrationComposition/**`,
`evidence/V1-FND-004/**`.

## Analysis

- None of the V1-FND-001 session's own writes appear in the findings list:
  - `ALKAROS.slnx` (modified) - allowlisted
  - `plan/v1/foundation/V1-FND-001-module-skeleton.md` (modified; Status/Assignee/scope lines) - allowlisted
  - `build/project-manifest.json` (modified; git-ignored under build/) - allowlisted
  - `evidence/V1-FND-001/solution-scope-update.txt` (new) - allowlisted
- The tool compares the whole working tree against one task's allowlist; two tasks
  run back-to-back on an uncommitted worktree therefore produce cross-task findings.
  The V1-FND-001 write-set itself is compliant.
- Verification commands executed and recorded in `solution-scope-update.txt`:
  - Host migration composition tests: 46/46 passed, exit 0
  - Architecture boundary tests: 4/4 passed, exit 0
  - `dotnet build ALKAROS.slnx`: exit 1, MSB4068 - the slnx format requires the
    .NET 9+ SDK; only SDK 8.0.423 is installed (global.json pins 8.0.423), so
    solution-level builds were not executable on this machine even before this change.
    The added slnx entry is format-identical to the existing entries.

## Conclusion

V1-FND-001 scope extension (register tests/Host test project in slnx + project
manifest) is implemented and compliant; the tool's false-negative is solely the
uncommitted V1-FND-004 worktree.

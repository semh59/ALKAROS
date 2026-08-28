# V1-GOV-007 validation results

- Repository root: `D:/PROJECT/ALKAROS`
- Candidate commit: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`
- Assignee: `/root/migration_manifest_custody`
- Exact write allowlist: V1-GOV-007 task file, V1-RMD-006 task file, V1-RMD-009 task file and
  `evidence/V1-GOV-007/**`.
- Production, test, migration, build, CI and configuration files changed by this task: `0`.

## Custody result

- V1-RMD-006 remains `Blocked`; its existing blockers remain and the two migration-composition paths were removed
  from its Owned surface.
- V1-RMD-009 remains `Planned`; both exact paths remain in its Owned surface and V1-GOV-007 was added to its
  dependency chain.
- Exact path search found both paths only in V1-RMD-009 among these two remediation tasks.

## Commands

`python -B tools/plan-audit/plan_audit_tool.py validate`

- Exit code: `0`
- Markdown files: `402`
- Task files: `380`
- Dependency edges: `1325`
- Validation errors: `0`
- Validation warnings: `0`

`git diff --check -- plan/v1/remediation/V1-RMD-006-production-dual-screen-pos.md plan/v1/remediation/V1-RMD-009-sql-persistence-concurrency-and-invariants.md`

- Exit code: `0`

`python -B tools/task-scope/task_scope_tool.py --task-id V1-GOV-007 --repo-root . --format text`

- Exit code: `1`
- Result: fail-closed. The tool reports the pre-existing global dirty/untracked worktree as outside this task's
  allowlist and also reports the new uncommitted task Markdown as having no committed baseline. This result is not
  presented as a pass. The separated pre/post hashes are in `pre-post-sha256.txt`; the immediately preceding global
  baseline is `evidence/V1-GOV-006/preflight-write-set.sha256`.

Git status emitted the pre-existing inaccessible `.pytest_cache/` permission warning; this task did not modify that
directory.

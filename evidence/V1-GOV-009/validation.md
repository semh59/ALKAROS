# V1-GOV-009 Validation

## Completed checks

| Check | Result |
| --- | --- |
| `python -B tools/plan-audit/plan_audit_tool.py validate` | Exit 0; 405 Markdown files, 383 task files, 0 errors, 0 warnings |
| `markdownlint-cli2@0.23.2` on the decision, task and evidence files | Exit 0; no output |
| `git diff --check` on the task allowlist | Exit 0; no output |

The bundled Node executable ran the exact cached `markdownlint-cli2@0.23.2` package because `node` was not available
on the process `PATH`. The first `npx.cmd` and `pnpm.cmd dlx` attempts failed before lint execution for that environment
reason; they were not counted as successful checks.

## Scope result

Task-owned/untracked paths are limited to:

- `docs/product/V1_PRODUCTION_EXPERIENCE_DECISION_2026-08-25.md`
- `evidence/V1-GOV-009/**`
- `plan/v1/governance/V1-GOV-009-production-experience-realignment.md`

The repository's other tracked and untracked audit/remediation changes remain outside this task and were not altered.

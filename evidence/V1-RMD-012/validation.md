# V1-RMD-012 Validation

- Task: `V1-RMD-012`
- Assignee: `/root/rmd012_markdownlint_cleanup`
- Repository root: `D:/PROJECT/ALKAROS`
- Candidate base: `a03d02146961c29a8b847a7b0c472c6c8dd42c9f`

## Preflight

The initial `git status --short` and `git diff --name-only` contained unrelated, pre-existing changes. This task
modified only its exact owned Markdown paths and this evidence directory. It did not alter or revert any unrelated
path.

## Markdownlint

Canonical repository command from `.markdownlint-cli2.jsonc` and V1-RMD-007:

```text
pnpm dlx markdownlint-cli2@0.23.2
markdownlint-cli2 v0.23.2 (markdownlint v0.41.1)
Finding: plan/**/*.md docs/**/*.md evidence/**/*.md AGENTS.md
Linting: 570 files
Summary: 0 issues in 0 files
EXIT_CODE=0
```

Passing `"**/*.md"` explicitly is not equivalent to the repository command: it adds `.agents/**`, `DESIGN.md`, and
nested generated `node_modules/**` files outside the configured root-lint surface. That diagnostic invocation returned
exit code `1` with 10,265 pre-existing out-of-scope findings and was not used as the V1-RMD-007 root-lint verdict.

## Plan and whitespace validation

```text
python -B tools/plan-audit/plan_audit_tool.py validate
Markdown files: 403
Task files: 381
Registered gates: 18
Registered EXT sources: 21
Dependency edges: 1326
Validation errors: 0
Validation warnings: 0
EXIT_CODE=0
```

```text
git diff --check -- docs/audit/FULL_PROJECT_PRODUCTION_AUDIT_2026-08-24.md \
  evidence/V1-RMD-004/validation.md evidence/V1-RMD-005/validation.md \
  plan/v1/remediation/V1-RMD-006-production-dual-screen-pos.md \
  plan/v1/remediation/V1-RMD-012-root-markdownlint-cleanup.md
EXIT_CODE=0
```

Global `git diff --check` remains exit code `2` only because of the pre-existing, out-of-scope trailing whitespace at
`src/Host/DualScreen/DualScreenStore.cs:262`. V1-RMD-012 did not modify that path.

## SHA-256

```text
8BD90477C1DE1F6AB9DD38F62FC47DAB1EE424B4DF378C1D09112530FA27E7B8  docs/audit/FULL_PROJECT_PRODUCTION_AUDIT_2026-08-24.md
454815D5CE0F2D2B78CEF42C7058671E86C999DA64B403E00D9AD604DBB0A7F9  evidence/V1-RMD-004/validation.md
8B46AF489E0E129215A39E8131E84863346D0AF514E21B7EBA98A3063EB6FB1B  evidence/V1-RMD-005/validation.md
A793D836C31D2DABB497A69D42B3DC3E63416863527944AEC938BBA238BFF5FF  plan/v1/remediation/V1-RMD-006-production-dual-screen-pos.md
912DC7AAA6EA6F06ED2E98CB0E0D34A9FB75042378F2154CE7EEB6356287BDF4  plan/v1/remediation/V1-RMD-012-root-markdownlint-cleanup.md
```

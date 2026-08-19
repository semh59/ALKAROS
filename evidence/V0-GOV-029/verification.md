# V0-GOV-029 Verification

- Date: 2026-08-03
- Task: V0-GOV-029
- Result: Passed

## Commands

- `npx --yes markdownlint-cli2@0.23.2 "plan/**/*.md" "docs/**/*.md" "AGENTS.md"` — exit `0`, plan/docs/AGENTS lint `0`
  issues.
- `python tools/plan-audit/plan_audit_tool.py generate-audit-report` — exit `0`, report lines `1669`.
- `python tools/plan-audit/plan_audit_tool.py generate-manifest` — exit `0`, manifest SHA-256
  `C22E5C7BEDCA3363E61E9CD548D32AEADCC40ED9223906438E3349AF0247E75D`.
- `python tools/plan-audit/plan_audit_tool.py validate` — exit `0`, `Validation errors: 0`, `Validation warnings: 0`.
- `python tools/plan-audit/plan_audit_tool.py validate-coverage` — exit `0`, `Coverage errors: 0`.
- `python tools/plan-audit/plan_audit_tool.py verify-manifest` — exit `0`, `Manifest errors: 0`.
- `py -m pytest tests/Architecture/TaskScope -q` — exit `0`, `73 passed in 51.19s`.
- `dotnet test ALKAROS.slnx` (with `ALKAROS_TEST_PG_*` container env) — exit `0`,
  `258 passed / 0 failed` (Secrets 21, SensitiveData 23, Transactions 25,
  Idempotency 71, TransactionOutboxIntegration 12, Identity.Authentication 41,
  Architecture 5, Host 60).

## Scope

- `HostServiceRegistrationTests.cs` recorded with single owner `V0-GOV-015` in
  `plan/v0/governance/V0-GOV-015-atomic-migration-history.md`.
- `plan/EXECUTION_READY_PLAN.md` separates dated ENV-003 historical evidence
  (2026-08-03, old counts) from current re-run results (Architecture 5/5,
  Host 60/60, Idempotency 71/71, TaskScope 73/73); old counts are not
  presented as current.
- `plan/AUDIT_REPORT.md` and `plan/AUDIT_MANIFEST.json` regenerated from the
  current Markdown tree; all plan/coverage/manifest validations exit 0.
- Root markdownlint still reports issues in `evidence/` files owned by
  other tasks (ENV-001, ENV-003, V1-FND-001, V1-FND-002, V1-FND-005,
  V1-IAM-001, V1-SEC-003); out of V0-GOV-029 scope.

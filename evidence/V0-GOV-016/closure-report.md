# V0-GOV-016 Closure Report

- Date: 2026-08-03
- Task: `V0-GOV-016`
- Result: `Done`

## Commands

```text
py tools/plan-audit/plan_audit_tool.py generate-audit-report
Exit code: 0
Audit findings recorded: 1827
Audit report lines: 1669

py tools/plan-audit/plan_audit_tool.py generate-manifest
Exit code: 0
Manifest Markdown files: 393
Manifest SHA-256: C22E5C7BEDCA3363E61E9CD548D32AEADCC40ED9223906438E3349AF0247E75D

py tools/plan-audit/plan_audit_tool.py verify-manifest
Exit code: 0

git diff --check
Exit code: 0
```

## Result

Post-remediation audit refresh: audit report ve manifest, 2026-08-03 V0
kapanis batch'i sonrasi yeniden uretildi ve verify-manifest ile eslendi.
Baseline audit records 211, added 182; no drift.

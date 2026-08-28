# V1-GOV-013 - Table card custody correction

- Task ID: V1-GOV-013
- Status: Done
- Assignee: /root
- Work type: documentation
- Surface state: Existing

## Goal

V1-GOV-012 doğrulamasında bulunan tarihsel table-folder ownership çakışmasını ve audit-report custody eksiğini,
remediation kapsamını genişletmeden exact plan yüzeyleriyle düzeltmek.

## Owned surface

- `plan/v1/governance/V1-GOV-012-table-card-remediation-custody.md`
- `plan/v1/remediation/V1-RMD-017-table-workspace-ui.md`
- `plan/v1/README.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V1-GOV-013/**`

## Dependencies

- V1-GOV-010

## Acceptance evidence

- V1-RMD-017 klasör wildcard'ı yalnız V1-RMD-022'ye devredilmeyen mevcut table feature dosyalarına daraltılır.
- V1-GOV-012 audit report ve manifesti birlikte yeniden üretmek için gereken exact custody yolunu kazanır; goal,
  remediation source yüzeyi veya dependency zinciri değişmez.
- `python -B tools/plan-audit/plan_audit_tool.py validate`, `verify-manifest`, root markdownlint ve
  `git diff --check` exit code `0` verir.

## Handoff

- V1-GOV-012

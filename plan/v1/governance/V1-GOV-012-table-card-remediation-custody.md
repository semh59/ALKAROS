# V1-GOV-012 - Table card remediation custody

- Task ID: V1-GOV-012
- Status: Done
- Assignee: /root
- Work type: documentation
- Surface state: Existing

## Goal

V1-RMD-010 gerçek browser denetiminde doğrulanan masa kartı klavye ve nested-interactive semantik bulgusunu,
değişmeyecek exact source ownership ve yeniden kabul zinciri bulunan tek bir remediation görevine dönüştürmek.

## Owned surface

- `plan/v1/remediation/V1-RMD-022-table-card-keyboard-semantics.md`
- `plan/v1/README.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V1-GOV-012/**`

## Dependencies

- V1-GOV-010

## Acceptance evidence

- V1-RMD-022 yalnız `TableWorkspace.tsx`, aynı bileşenin testi, `tables.css` ve kendi evidence dizisini sahiplenir.
- Görev V1-RMD-020'ye bağlanır; başarılı kaynak düzeltmesi V1-RMD-010 yeniden kabulüne devredilir.
- `python -B tools/plan-audit/plan_audit_tool.py validate`, `verify-manifest`, root markdownlint ve
  `git diff --check` exit code `0` verir.

## Handoff

- V1-RMD-022

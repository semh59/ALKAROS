# V1-GOV-010 - Production experience task custody

- Task ID: V1-GOV-010
- Status: Done
- Assignee: /root/gov010_resume
- Work type: documentation
- Surface state: Planned

## Goal

V1-GOV-009 ürün deneyimi kararını non-overlapping exact ownership, dependency ve acceptance sınırları bulunan
uygulanabilir remediation görevlerine dönüştürmek; mevcut audit sırasını yeni production shell zincirine bağlamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-013-table-management-production-api.md`
- `plan/v1/remediation/V1-RMD-014-catalog-management-production-api.md`
- `plan/v1/remediation/V1-RMD-015-kitchen-operations-production-api.md`
- `plan/v1/remediation/V1-RMD-016-production-shell-and-design-system.md`
- `plan/v1/remediation/V1-RMD-017-table-workspace-ui.md`
- `plan/v1/remediation/V1-RMD-018-menu-catalog-workspace-ui.md`
- `plan/v1/remediation/V1-RMD-019-kitchen-operations-workspace-ui.md`
- `plan/v1/remediation/V1-RMD-020-production-experience-composition-and-e2e.md`
- `plan/v1/remediation/V1-RMD-021-independent-designer-acceptance.md`
- `plan/v1/remediation/V1-RMD-010-pos-terminal-ui-responsive-and-a11y.md`
- `plan/v1/remediation/V1-RMD-011-web-prototype-hardening-and-touch-targets.md`
- `plan/v1/governance/V1-GOV-004-post-remediation-master-audit-reseal.md`
- `plan/v1/README.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/v1/remediation/V1-RMD-009-sql-persistence-concurrency-and-invariants.md`
- `plan/AUDIT_REPORT.md`
- `evidence/V1-GOV-010/**`

## Dependencies

- V1-GOV-009

## Acceptance evidence

- V1-RMD-013..021 task dosyaları V1-GOV-009 kararındaki contract, manual scenario ve kalite sınırlarını kaybetmeden
  tek sorumluluk ve non-overlapping exact owned surface ile oluşturulur.
- Sıra `V1-RMD-013..016` bağımsız uygun dallar, `V1-RMD-017..019` ilgili API/shell bağımlılıkları,
  `V1-RMD-020`, `V1-RMD-010`, `V1-RMD-011`, `V1-RMD-021`, `V1-GOV-004` olarak fail-closed bağlanır.
- V1-RMD-010 mevcut değişiklikleri silmeden yeni integrated shell sonrasına alınır; WebPrototype yalnız karantina görevi
  olarak kalır ve production evidence sayılmaz.
- `plan/v1/README.md` görev sayımları ve `plan/AUDIT_MANIFEST.json` yeni task yüzeyiyle yeniden üretilir.
- `python -B tools/plan-audit/plan_audit_tool.py validate`, `verify-manifest`, root markdownlint ve `git diff --check`
  exit code `0` verir.

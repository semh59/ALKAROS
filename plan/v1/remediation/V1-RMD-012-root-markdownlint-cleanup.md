# V1-RMD-012 - Root Markdownlint Cleanup

- Task ID: V1-RMD-012
- Status: Done
- Assignee: /root/rmd012_markdownlint_cleanup
- Work type: documentation
- Surface state: Existing

## Goal

V1-RMD-007 kök lint kapısını engelleyen dört mevcut Markdown dosyasındaki dokuz biçim ihlalini, içerik anlamını ve
görev durumlarını değiştirmeden gidermek.

## Owned surface

- `plan/v1/remediation/V1-RMD-012-root-markdownlint-cleanup.md`
- `docs/audit/FULL_PROJECT_PRODUCTION_AUDIT_2026-08-24.md`
- `evidence/V1-RMD-004/validation.md`
- `evidence/V1-RMD-005/validation.md`
- `plan/v1/remediation/V1-RMD-006-production-dual-screen-pos.md`
- `evidence/V1-RMD-012/**`

## In scope

- `markdownlint-cli2@0.23.2` tarafından raporlanan MD013 ve MD036 ihlallerini mekanik reflow ve heading düzeltmesiyle
  kapatmak.

## Out of scope

- Rapor hükmünü, kanıt sonuçlarını veya V1-RMD-006 görev metadata, dependency, blocker ve owned-surface anlamını
  değiştirmek.
- V1-RMD-007 kapsamındaki CI, provenance, coverage, SBOM/license ve history-secret kanıtlarını üretmek.

## Dependencies

- V1-GOV-003

## Acceptance evidence

- `pnpm dlx markdownlint-cli2@0.23.2 "**/*.md" "#node_modules" "#**/bin/**" "#**/obj/**"` exit code `0` verir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.
- `git diff --check` bu görevin owned Markdown yüzeyinde exit code `0` verir.
- Semih, dört hedef dosyanın metinsel anlamının ve V1-RMD-006 `Blocked` durumunun değişmediğini diff üzerinden
  doğrulayabilir.

## Handoff

- V1-RMD-007

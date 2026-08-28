# V1-GOV-019 - Desktop POS custody recovery

- Task ID: V1-GOV-019
- Status: Done
- Assignee: /root
- Work type: documentation
- Surface state: Existing

## Goal

V1-GOV-017 ve V1-GOV-018 blocker kayıtlarını koruyarak, doğrulanmış historical task yollarında exact üretim yüzeyi
transferini tamamlamak ve V1-RMD-026..032 zincirini plan kapılarından geçirilebilir hale getirmek.

## Owned surface

- `plan/v1/governance/V1-GOV-017-desktop-pos-and-container-release-custody.md`
- `plan/v1/governance/V1-GOV-018-desktop-pos-custody-correction.md`
- `plan/v1/governance/V1-GOV-019-desktop-pos-custody-recovery.md`
- `plan/v1/billing/V1-BIL-004-billing-composite-integrity.md`
- `plan/v1/foundation/V1-FND-022-table-module-integration.md`
- `plan/v1/table-management/V1-TBL-001-table-lifecycle.md`
- `plan/v1/remediation/V1-RMD-002-deep-audit-remediation.md`
- `plan/v1/remediation/V1-RMD-006-production-dual-screen-pos.md`
- `plan/v1/remediation/V1-RMD-009-sql-persistence-concurrency-and-invariants.md`
- `plan/v1/remediation/V1-RMD-013-table-management-production-api.md`
- `plan/v1/remediation/V1-RMD-017-table-workspace-ui.md`
- `plan/v1/remediation/V1-RMD-018-menu-catalog-workspace-ui.md`
- `plan/v1/remediation/V1-RMD-020-production-experience-composition-and-e2e.md`
- `plan/v1/remediation/V1-RMD-022-table-card-keyboard-semantics.md`
- `plan/v1/remediation/V1-RMD-023-production-landmark-and-aria-semantics.md`
- `plan/v1/remediation/V1-RMD-026-floor-plan-persistence-and-api.md`
- `plan/v1/remediation/V1-RMD-027-operational-bill-splitting-api.md`
- `plan/v1/remediation/V1-RMD-028-desktop-floor-plan-workspace.md`
- `plan/v1/remediation/V1-RMD-029-seat-aware-order-and-bill-split-workspace.md`
- `plan/v1/remediation/V1-RMD-030-menu-management-desktop-quality.md`
- `plan/v1/remediation/V1-RMD-031-complete-containerized-release.md`
- `plan/v1/remediation/V1-RMD-032-integrated-designer-and-release-acceptance.md`
- `plan/v1/README.md`
- `plan/AUDIT_MANIFEST.json`
- `plan/AUDIT_REPORT.md`
- `evidence/V1-GOV-019/**`

## Dependencies

- V1-GOV-009
- V1-GOV-016

## Acceptance evidence

- Historical task kapanışları korunur; devredilen exact paths eski owner listelerinden çıkarılır ve tarihli transfer
  notuyla V1-RMD-026..031'e bağlanır.
- V1-GOV-017 ve V1-GOV-018 `Blocked` kayıtları ve blocker nedenleri değişmeden kalır; V1-RMD-026..032 Türkçe görev
  sözleşmesine uyar.
- Plan validation ownership, dependency, source ve dil hatası üretmez. Manifest ve audit report deterministik olarak
  yeniden üretilip doğrulanır.
- Markdown lint ve `git diff --check` exit code `0` verir; allowlist dışı yol değişmez.

## Handoff

- V1-RMD-026

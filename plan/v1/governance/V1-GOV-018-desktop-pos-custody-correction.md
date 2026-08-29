# V1-GOV-018 - Desktop POS custody correction

- Task ID: V1-GOV-018
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: documentation
- Surface state: Existing

## Goal

V1-GOV-017 doğrulamasında bulunan eski/yeni owned-surface çakışmalarını, historical görev kayıtlarından exact transfer
ile gidermek; V1-RMD-026..032 görevlerini Türkçe plan sözleşmesine uygun ve çalıştırılabilir hale getirmek.

## Owned surface

- `plan/v1/governance/V1-GOV-017-desktop-pos-and-container-release-custody.md`
- `plan/v1/governance/V1-GOV-018-desktop-pos-custody-correction.md`
- `plan/v1/billing/V1-BIL-004-billing-composite-integrity.md`
- `plan/v1/foundation/V1-FND-022-table-repository-concurrency.md`
- `plan/v1/table-management/V1-TBL-001-table-and-zone-lifecycle.md`
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
- `evidence/V1-GOV-018/**`

## Dependencies

- V1-GOV-009
- V1-GOV-016

## Acceptance evidence

- Historical görevlerin kapanış içeriği korunur; yalnız V1-RMD-026..032 tarafından devralınan exact owned-surface
  satırları açık transfer notuyla kaldırılır veya daraltılır.
- V1-GOV-017 blocker kaydı tarihsel olarak korunur ve yeni görevlerin hedef/sahiplik metni Türkçe görev sözleşmesine
  uyar.
- Yeni görevlerde iki bağımsız davranış tek task altında birleşmez; migration/API, floor UI, split UI, katalog,
  container release ve son doğrulama sıralı kalır.
- Plan validation, manifest generation/verification, Markdown lint ve `git diff --check` exit code `0` verir.

## Handoff

- V1-RMD-026

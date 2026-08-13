# V0-GOV-044 - Close the Markdown lint failures

- Task ID: V0-GOV-044
- Status: Done
- Assignee: /root/implement_v0_gov_044
- Work type: documentation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

Dondurulmuş mandatory Markdown lint kontrolündeki exact ihlalleri içerik anlamını ve historical evidence iddialarını
değiştirmeden düzeltmek.

## Owned surface

- `evidence/ENV-001/env001-sdk-repair.md`
- `evidence/ENV-003/env003-test-matrix.md`
- `evidence/V0-GOV-030/verification.md`
- `evidence/V0-GOV-031/verification.md`
- `evidence/V1-FND-001/defect-1-closure.md`
- `evidence/V1-FND-001/verification.md`
- `evidence/V1-FND-002/defect-3-closure.md`
- `evidence/V1-FND-002/defect-4-closure.md`
- `evidence/V1-FND-002/defect-6-closure.md`
- `evidence/V1-FND-003/verification.md`
- `evidence/V1-FND-004/verification.md`
- `evidence/V1-FND-005/defect-5-closure.md`
- `evidence/V1-FND-005/verification.md`
- `evidence/V1-FND-009/closure-report-2026-08-05.md`
- `evidence/V1-FND-013/verification.md`
- `evidence/V1-FND-014/verification.md`
- `evidence/V1-FND-015/verification.md`
- `evidence/V1-IAM-001/closure-2026-08-05.md`
- `evidence/V1-IAM-001/defect-7-closure.md`
- `evidence/V1-IAM-002/closure-2026-08-08.md`
- `evidence/V1-IAM-003/audit-slnx-regression-2026-08-09.md`
- `evidence/V1-IAM-003/closure-2026-08-08.md`
- `evidence/V1-IAM-003/manifest-recover-2026-08-09.md`
- `evidence/V1-IAM-004/closure-2026-08-08.md`
- `evidence/V1-IAM-005/verification.md`
- `evidence/V1-SEC-003/closure-2026-08-05.md`
- `evidence/V1-SEC-003/defect-2-closure.md`
- `plan/v0/governance/V0-GOV-031-c42-entry-gate-approval.md`
- `evidence/V0-GOV-044/**`

2026-08-13 user scope extension ("hepsini düzelt"): canlı lint ölçümü 74 dosyada 281 hata
gösterdi; owned surface aşağıdaki gerçek hatalı dosyalarla genişletildi.

- `evidence/V0-GOV-037/controls.md`
- `evidence/V0-GOV-038/controls.md`
- `evidence/V0-GOV-061/checkpoint.md`
- `plan/TRACEABILITY.md`
- `plan/v0/governance/V0-GOV-015-atomic-migration-history.md`
- `plan/v0/governance/V0-GOV-036-decision-gate-reconciliation.md`
- `plan/v0/governance/V0-GOV-037-task-traceability-reconciliation.md`
- `plan/v0/governance/V0-GOV-038-immutable-history-attestation.md`
- `plan/v0/governance/V0-GOV-039-closure-evidence-envelope.md`
- `plan/v0/governance/V0-GOV-040-project-manifest-consistency.md`
- `plan/v0/governance/V0-GOV-041-github-ci-protection.md`
- `plan/v0/governance/V0-GOV-042-code-coverage-gate.md`
- `plan/v0/governance/V0-GOV-043-dotnet-format-remediation.md`
- `plan/v0/governance/V0-GOV-044-markdownlint-remediation.md`
- `plan/v0/governance/V0-GOV-045-head-manifest-integrity.md`
- `plan/v0/governance/V0-GOV-046-manifest-semantic-separation.md`
- `plan/v0/governance/V0-GOV-047-build-provenance.md`
- `plan/v0/governance/V0-GOV-048-final-remediation-audit.md`
- `plan/v0/governance/V0-GOV-061-v3-closure-fixed-final-verification.md`
- `plan/v1/catalog/V1-CAT-003-nonnegative-current-price.md`
- `plan/v1/catalog/V1-CAT-004-catalog-baseline-reacceptance.md`
- `plan/v1/foundation/V1-FND-002-idempotency-infrastructure.md`
- `plan/v1/foundation/V1-FND-015-inbox-idempotency-contract.md`
- `plan/v1/foundation/V1-FND-016-host-module-reachability.md`
- `plan/v1/foundation/V1-FND-017-host-data-source-bootstrap.md`
- `plan/v1/foundation/V1-FND-018-atomic-idempotency-execution.md`
- `plan/v1/foundation/V1-FND-019-message-lease-fencing.md`
- `plan/v1/foundation/V1-FND-020-rollback-exhaustion.md`
- `plan/v1/foundation/V1-FND-021-postgresql-extension-integration.md`
- `plan/v1/foundation/V1-FND-022-table-module-integration.md`
- `plan/v1/identity-authorization/V1-IAM-001-authentication.md`
- `plan/v1/identity-authorization/V1-IAM-002-authorization.md`
- `plan/v1/identity-authorization/V1-IAM-005-login-timing-contract.md`
- `plan/v1/identity-authorization/V1-IAM-006-reconnect-operation-claiming.md`
- `plan/v1/identity-authorization/V1-IAM-007-session-revocation-linearization.md`
- `plan/v1/identity-authorization/V1-IAM-008-authorization-linearization.md`
- `plan/v1/identity-authorization/V1-IAM-009-password-iteration-bounds.md`
- `plan/v1/identity-authorization/V1-IAM-010-login-work-factor-contract.md`
- `plan/v1/identity-authorization/V1-IAM-011-login-session-integration.md`
- `plan/v1/identity-authorization/V1-IAM-012-device-session-lifetime.md`
- `plan/v1/identity-authorization/V1-IAM-013-issued-token-redaction.md`
- `plan/v1/identity-authorization/V1-IAM-014-identity-baseline-reacceptance.md`
- `plan/v1/security-foundation/V1-SEC-004-migration-secret-redaction.md`
- `plan/v1/security-foundation/V1-SEC-005-immutable-data-classification.md`
- `plan/v1/security-foundation/V1-SEC-006-sanitized-handler-errors.md`
- `plan/v1/table-management/V1-TBL-006-table-lifecycle-reacceptance.md`

## In scope

- Mandatory lint çıktısındaki exact file/rule çiftlerini yeniden ölçmek.
- Yalnız whitespace, heading, list, fence ve line-length gibi semantic olmayan Markdown biçimini düzeltmek.
- TRACEABILITY değişikliğini `V0-GOV-037` tamamlandıktan sonra uygulamak.

## Out of scope

- Evidence verdict, command, exit code, tarih veya hash iddiasını yeniden yazmak.
- Lint config'i gevşetmek, dosya hariç tutmak veya unrelated Markdown formatlamak.

## Dependencies

- V0-GOV-035
- V0-GOV-037

## Deliverables

- Exact 74-path Markdown remediation ve before/after rule ledger (281 issue; 2026-08-13 canlı ölçüm).

## Acceptance evidence

- Zorunlu repository Markdown lint komutu timeout olmadan exit code `0` ve issue count `0` verir.
- Her değişiklik lint rule ile birebir bağlıdır; narrative/evidence semantiği için diff review yapılır.
- Plan validation ve task-scope kontrolü exit code `0` verir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; kanıtlar yalnız `evidence/V0-GOV-044/**`
  altındadır.

## Handoff

- V0-GOV-041
- V0-GOV-045

# V0-GOV-034 - Materialize the C52 remediation task graph

- Task ID: V0-GOV-034
- Status: InProgress
- Assignee: 019fea95-a9d0-78a1-887c-5544a4d1b19f
- Work type: documentation
- Surface state: Planned

## Source basis

- CORR:C52

## Goal

Doğrulanmış routing ledger'ındaki owner kimliklerini tek-sorumluluklu görev
Markdown dosyalarına dönüştürmek; exact owned surface, dependency, blocker ve
acceptance sözleşmelerini üretim yazımı başlamadan kilitlemek.

## Owned surface

- `plan/v0/governance/V0-GOV-035-remediation-admission-control.md`
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
- `plan/v0/revalidation/V0-REV-001-dat001-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-002-dat002-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-003-dat003-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-004-dat004-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-005-dat005-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-006-dat006-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-007-doc001-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-008-dom001-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-009-dom002-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-010-dom003-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-011-dom004-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-012-dom005-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-013-dom006-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-014-dom007-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-015-dom008-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-016-dom009-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-017-dom010-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-018-dom011-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-019-lic001-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-020-arc001-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-021-arc002-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-022-arc003-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-023-arc004-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-024-arc005-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-025-arc006-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-026-arc007-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-027-arc008-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-028-arc009-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-029-cmp002-decision-revalidation.md`
- `plan/v0/revalidation/V0-REV-030-cmp005-decision-revalidation.md`
- `plan/v0/data-architecture/V0-DAT-007-postgresql-extension-ownership.md`
- `plan/v1/foundation/V1-FND-016-host-module-reachability.md`
- `plan/v1/foundation/V1-FND-017-host-data-source-bootstrap.md`
- `plan/v1/foundation/V1-FND-018-atomic-idempotency-execution.md`
- `plan/v1/foundation/V1-FND-019-message-lease-fencing.md`
- `plan/v1/foundation/V1-FND-020-rollback-exhaustion.md`
- `plan/v1/foundation/V1-FND-021-postgresql-extension-integration.md`
- `plan/v1/foundation/V1-FND-022-table-module-integration.md`
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
- `plan/v1/catalog/V1-CAT-003-nonnegative-current-price.md`
- `plan/v1/catalog/V1-CAT-004-catalog-baseline-reacceptance.md`
- `plan/v1/table-management/V1-TBL-006-table-lifecycle-reacceptance.md`
- `plan/TRACEABILITY.md`
- `plan/AUDIT_REMEDIATION_ROUTING.csv`
- `plan/AUDIT_REMEDIATION_ROUTING.json`
- `evidence/V0-GOV-034/**`

## In scope

- Ledger'daki her owner Task ID için zorunlu metadata ve bölüm sırasına sahip
  bir görev dosyası üretmek.
- Eski sahiplerden devralınacak exact yolları her yeni görevde ayrı belirtmek.
- Aynı production veya global manifest yüzeyini kullanan görevleri dependency
  ile seri hale getirmek.
- Dış onay gerektiren 30 decision revalidation görevini kanıt gelene kadar
  fail-closed `Blocked` tanımlamak.

## Out of scope

- Child görevlerde tarif edilen ürün, test, migration, CI veya validator
  değişikliklerini uygulamak.
- Eski `Done` görev gövdelerini değiştirmek, migration geçmişini yeniden yazmak
  veya kullanıcı dirty çalışma ağacını temizlemek.

## Dependencies

- V0-GOV-033

## Deliverables

- Routing ledger ile birebir eşleşen yeni görev dosyaları ve acyclic dependency
  DAG'ı.
- Her production yüzeyi için tek aktif owner ve çakışan yüzeyler için kesin
  sıralama.
- Gerçek dış kaynak veya named approver bekleyen görevlerde açık kaldırılma
  koşullu blocker kaydı.

## Acceptance evidence

- Routing ledger owner kimliklerinin her biri tam olarak bir görev dosyasına
  karşılık gelir; eksik ve duplicate kimlik sayısı `0` olur.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.
- Child görevlerden hiçbiri `InProgress` veya `Done` yapılmaz.
- Değişen yollar bu görevin exact allowlist'iyle birebir eşleşir ve kanıtlar
  `evidence/V0-GOV-034/**` altında kayıtlıdır.

## Handoff

- V0-GOV-035

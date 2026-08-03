# V0-GOV-018 - Reconcile invalid Done statuses

- Task ID: V0-GOV-018
- Status: Blocked
- Assignee: /root
- Work type: validation
- Surface state: Existing

## Source basis

- CORR:C37

## Goal

Açık doğrudan veya transitive dependency zinciri taşıyan tarihsel `Done`
görevlerini candidate evidence korunarak `Blocked` durumuna geri almak.

## Owned surface

- `plan/v0/platform-architecture/V0-ARC-002-local-first-sync-contract.md`
- `plan/v0/platform-architecture/V0-ARC-003-idempotency-inbox-outbox.md`
- `plan/v0/platform-architecture/V0-ARC-005-settings-and-secret-classification.md`
- `plan/v0/platform-architecture/V0-ARC-006-notification-delivery-matrix.md`
- `plan/v0/platform-architecture/V0-ARC-007-deployment-compatibility-matrix.md`
- `plan/v0/platform-architecture/V0-ARC-008-release-evidence-contract.md`
- `plan/v0/platform-architecture/V0-ARC-009-qr-relay-topology.md`
- `plan/v0/compliance/V0-CMP-003-kvkk-data-inventory.md`
- `plan/v0/data-architecture/V0-DAT-001-migration-dependency-graph.md`
- `plan/v0/data-architecture/V0-DAT-003-nullable-unique-policy.md`
- `plan/v0/data-architecture/V0-DAT-004-projection-ownership.md`
- `plan/v0/data-architecture/V0-DAT-005-single-branch-key-strategy.md`
- `plan/v0/data-architecture/V0-DAT-006-migration-rehearsal-profile.md`
- `plan/v0/domain-contracts/V0-DOM-005-table-reservation-policy.md`
- `plan/v0/domain-contracts/V0-DOM-006-void-complimentary-discount-policy.md`
- `plan/v0/domain-contracts/V0-DOM-007-customer-credit-invoice-semantics.md`
- `plan/v0/domain-contracts/V0-DOM-008-reporting-metric-contract.md`
- `plan/v0/domain-contracts/V0-DOM-009-receipt-variance-policy.md`
- `plan/v0/domain-contracts/V0-DOM-010-inventory-cost-basis.md`
- `plan/v0/governance/V0-GOV-010-task-scope-root-normalization.md`
- `plan/v0/governance/V0-GOV-011-final-audit-manifest-refresh.md`
- `plan/v0/governance/V0-GOV-012-final-gate-and-audit-reconciliation.md`
- `plan/v0/governance/V0-GOV-013-sensitive-envelope-metadata-integrity.md`
- `plan/v0/governance/V0-GOV-014-messaging-retry-backoff.md`
- `plan/v0/governance/V0-GOV-015-atomic-migration-history.md`
- `plan/v0/governance/V0-GOV-016-post-remediation-audit-refresh.md`
- `plan/v1/foundation/V1-FND-001-module-skeleton.md`
- `plan/v1/foundation/V1-FND-002-idempotency-infrastructure.md`
- `plan/v1/foundation/V1-FND-003-codex-task-scope-enforcement.md`
- `plan/v1/foundation/V1-FND-004-host-migration-composition.md`
- `plan/v1/foundation/V1-FND-005-transaction-execution-boundary.md`
- `plan/v1/foundation/V1-FND-006-transaction-outbox-integration.md`
- `plan/v1/foundation/V1-FND-007-audit-remediation.md`
- `plan/v1/foundation/V1-FND-008-audit-remediation-round2.md`
- `plan/v1/foundation/V1-FND-009-rewrite-pushed-history.md`
- `plan/v1/foundation/V1-FND-011-transaction-outbox-atomicity.md`
- `plan/v1/foundation/V1-FND-012-runtime-migration-manifest.md`
- `plan/v1/identity-authorization/V1-IAM-001-authentication.md`
- `plan/v1/identity-authorization/V1-IAM-004-concurrent-lockout.md`
- `plan/v1/security-foundation/V1-SEC-001-secret-resolution-boundary.md`
- `plan/v1/security-foundation/V1-SEC-002-sensitive-payload-boundary.md`
- `plan/v1/security-foundation/V1-SEC-003-host-database-secret-input.md`
- `evidence/v0/gate-v0-exit-closure.md`
- `plan/AUDIT_REPORT.md`
- `plan/AUDIT_MANIFEST.json`
- `evidence/V0-GOV-018/**`

## In scope

- Status/Assignee/Blocker metadata'sı, candidate evidence açıklaması, V0 gate
  sayımı ve audit envanteri.

## Out of scope

- Production kodu, test, migration, task dependency listesi, kaynak PDF veya
  dış sağlayıcı davranışı.

## Dependencies

- V0-GOV-017

## Blocker

- Validator candidate evidence olarak korunan mevcut Git/kod ağacını erken
  application yüzeyi sayıyor. `V0-GOV-021` fail-closed runtime gate kuralını
  tamamlayınca görev yeniden `Planned` yapılabilir.

## Deliverables

- 42 task için yeniden doğrulanma blocker'ı, güncel açık V0 gate kaydı ve
  sıfır status-dependency ihlali gösteren audit artifact'leri.

## Acceptance evidence

- Eski kod, test ve evidence silinmez; yalnız candidate evidence olarak
  sınıflanır.
- `validate`, `validate-coverage` ve `verify-manifest` exit code `0` verir.
- V0 gate task sayımı metadata ile birebir eşleşir ve `Open` kalır.

## Handoff

- GATE-V0-EXIT

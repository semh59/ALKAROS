# V0-GOV-037 - Transfer C52 remediation ownership and custody

- Task ID: V0-GOV-037
- Status: Done
- Assignee: /root/c52_ownership_transfer_resume
- Work type: documentation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

Mevcut `Done` görevleri yeniden açmadan, düzeltilecek exact production/test/migration yüzeylerini yeni tek-sorumluluklu C52 görevlerine devretmek; routing, traceability ve dondurulmuş worktree custody kayıtlarını aynı owner grafiğinde uzlaştırmak.

## Owned surface

- `plan/TRACEABILITY.md`
- `plan/AUDIT_REMEDIATION_ROUTING.csv`
- `plan/AUDIT_REMEDIATION_ROUTING.json`
- `plan/v0/governance/V0-GOV-013-sensitive-envelope-metadata-integrity.md`
- `plan/v0/governance/V0-GOV-015-atomic-migration-history.md`
- `plan/v1/foundation/V1-FND-001-module-skeleton.md`
- `plan/v1/foundation/V1-FND-002-idempotency-infrastructure.md`
- `plan/v1/foundation/V1-FND-004-host-migration-composition.md`
- `plan/v1/foundation/V1-FND-011-transaction-outbox-atomicity.md`
- `plan/v1/foundation/V1-FND-012-runtime-migration-manifest.md`
- `plan/v1/foundation/V1-FND-013-host-composition-constructability.md`
- `plan/v1/foundation/V1-FND-014-retry-sql-identifier-hardening.md`
- `plan/v1/foundation/V1-FND-015-inbox-idempotency-contract.md`
- `plan/v1/identity-authorization/V1-IAM-001-authentication.md`
- `plan/v1/identity-authorization/V1-IAM-002-authorization.md`
- `plan/v1/identity-authorization/V1-IAM-003-device-sessions.md`
- `plan/v1/identity-authorization/V1-IAM-004-concurrent-lockout.md`
- `plan/v1/identity-authorization/V1-IAM-005-login-timing-contract.md`
- `plan/v1/security-foundation/V1-SEC-002-sensitive-payload-boundary.md`
- `plan/v1/security-foundation/V1-SEC-003-host-database-secret-input.md`
- `plan/v1/catalog/V1-CAT-001-product-catalog.md`
- `plan/v1/table-management/V1-TBL-001-table-lifecycle.md`
- `plan/v1/table-management/V1-TBL-002-table-transfer.md`
- `plan/v1/table-management/V1-TBL-003-table-merge.md`
- `plan/v1/table-management/V1-TBL-004-table-reservation-record.md`
- `plan/v1/table-management/V1-TBL-005-current-pointer-projection.md`
- `plan/v0/governance/V0-GOV-040-project-manifest-consistency.md`
- `plan/v0/governance/V0-GOV-043-dotnet-format-remediation.md`
- `plan/v0/governance/V0-GOV-047-build-provenance.md`
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
- `evidence/V0-GOV-037/**`

## In scope

- Eski 19 surface custodian task'ın `Owned surface` kayıtlarından yalnız C52 bulgularının değiştireceği exact yolları çıkarmak; untouched tracked production dosyalarının owner coverage'ını korumak.
- Devredilen yolları yeni C52 tasklerine tek owner olacak biçimde eklemek; aynı dosyada birden fazla düzeltme gerekiyorsa tek implementation/integration owner ve ayrı read-only validation task seçmek.
- `database/MigrationComposition/order.json`, Host composition, project/lock, Identity service ve messaging store gibi ortak yüzeyleri tek final integration sahibine bağlamak ve dependency DAG'ını seri hale getirmek.
- `V1-FND-015` tarafından sahiplenilen `InboxMessage.cs` lease-token değişikliğini `V1-FND-019` owner zincirine exact devretmek.
- Identity tasklerinde doğrulanan `Surface state`, Table tasklerinde owned/deliverable parity ve frozen 17 tracked + 15 untracked = 32 yol / 16 out-of-scope custody sonucunu uzlaştırmak.
- Eski tasklerin source/history metnini historical provenance olarak korumak; yeni remediation tasklerinin current source authority'sini yalnız `CORR:C52` tutmak.

## Out of scope

- Production, test, migration, solution, project, lock, validator veya CI dosyası değiştirmek.
- Mevcut `Done` task'ı yeniden açmak, status/assignee değerini değiştirmek, geçmiş commit'i rewrite etmek veya kullanıcı dirty worktree'sine yazmak.
- 45 tarihsel commit-scope ihlalini current dirty path sayısı gibi sunmak; bu kayıt `V0-GOV-038` kapsamındadır.

## Dependencies

- V0-GOV-033

## Deliverables

- Eski ve yeni task Markdown'larında exact, duplicate-free surface ownership transfer'i.
- Routing/task catalog/dependency kayıtlarıyla aynı yeni owner grafiği.
- SHA-256 içeren 32-path frozen custody ledger ve 19 eski task için status-preservation kanıtı.

## Acceptance evidence

- 19 eski task'ın tamamı `Done` ve mevcut assignee değeriyle kalır; yeni child tasklerin hiçbiri `InProgress` veya `Done` yapılmaz.
- Devredilen her production/test/migration yolu tam bir yeni implementation/integration owner'a sahiptir; exact duplicate ve prefix overlap sayısı `0` olur.
- Untouched tracked production coverage açığı, routing owner eksikliği ve task-catalog dependency farkı sayısı `0` olur.
- Custody ledger tam `32` yol ve `16` out-of-scope sonucu verir; 45 sayısı yalnız immutable history ledger'ında kalır.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir.
- Değişen yollar bu görevin exact allowlist'iyle eşleşir; kanıtlar yalnız `evidence/V0-GOV-037/**` altındadır.

## Handoff

- V0-GOV-034

# V1-RMD-001 - Consolidated remediation of independent audit findings

- Task ID: V1-RMD-001
- Status: Done
- Assignee: Antigravity-v1-rmd-001
- Work type: implementation
- Surface state: Existing

## Source basis

- CORR:C52

## Goal

Bağımsız denetimde kanıtlanmış bulguları tek implementation diff zincirinde düzeltmek; bu görev dört `NotApplicable`
kapanacak görevin (V1-IAM-006, V1-IAM-011, V1-CAT-003, V1-FND-020) ve tarihsel Done görev yüzeylerinin (V1-FND-011,
V1-SEC-003, V1-FND-022) devralınan yüzeylerini birlikte sahiplenir. C70 kullanıcı onaylı konsolidasyon kaydına göre
yürütülür; V1-IAM-008 kendi kod sahibi olarak kalır ve bu görevin yüzeyine girmez.

## Owned surface

- `src/BuildingBlocks/Transactions/TransactionContext.cs`
- `src/BuildingBlocks/Transactions/TransactionScope.cs`
- `src/BuildingBlocks/TransactionOutboxIntegration/TransactionOutbox.cs`
- `src/BuildingBlocks/TransactionOutboxIntegration/TransactionOutboxResource.cs`
- `src/Host/Program.cs`
- `src/Modules/Identity/Authentication/AuthenticationService.cs`
- `src/Modules/Identity/Authentication/LoginResult.cs`
- `src/Modules/Identity/Authentication/SessionTokenIssuer.cs`
- `src/Modules/Identity/DeviceSessions/DeviceSessionService.cs`
- `src/Modules/Catalog/ProductCatalog/Product.cs`
- `src/Modules/Catalog/ProductCatalog/PostgresProductRepository.cs`
- `database/MigrationComposition/order.json`
- `tests/Modules/Identity/DeviceSessions/DeviceSessionServiceTests.cs`
- `tests/Modules/Identity/Authentication/SessionTokenIssuerTests.cs`
- `tests/Modules/Identity/Authentication/AuthenticationServiceTests.cs`
- `tests/Modules/Identity/Authentication/AuthenticationTimingContractTests.cs`
- `tests/Modules/Catalog/ProductCatalog/DomainTests.cs`
- `tests/Modules/Catalog/ProductCatalog/PostgresRepositoryTests.cs`
- `tests/BuildingBlocks/Transactions/Execution/TransactionExecutionTests.cs`
- `database/migrations/V1/V1-CAT-003/014-catalog-current-price-bound.up.sql`
- `database/migrations/V1/V1-CAT-003/014-catalog-current-price-bound.down.sql`
- `evidence/V1-RMD-001/**`

## In scope

- `TransactionContext.cs:44,77`: cross-shadow join yolunda retryPolicy/cancellationToken kaybını kaldırıp restart
  `retryPolicy` ve caller cancellationToken'ın tek transaction execution'ına taşındığını doğrulamak.
- `TransactionScope.cs:87-88`: rollback callback hatası sonrası kayıtlı bütün rollback kaynaklarının tam bir kez
  `CancellationToken.None` ile denenmesi ve hataların aggregate edilmesi.
- `TransactionOutbox.cs`: post-commit wake-up eksikliğini kapatarak commit sonrası dispatch'in crash-safe tetiklenmesi.
- `AuthenticationService.cs:84`: known-invalid/unknown-user yol ayrımında orphan token/session üretimini kapamak.
- `DeviceSessionService.cs:55-76`: reconnect claim/replacement işlemlerini transaction içinde atomikleştirmek.
- `Product.cs` / `PostgresProductRepository.cs`: negative current price'ın domain ve PostgreSQL sınırında atomik
  reddi; migration up/down çifti ve repository constraint uyumu.
- `order.json:34`: `product_modifier_groups` pozisyon kaydını manifest ve `ManifestTests` ile birebir eşleşecek şekilde
  tamamlamak.
- `Program.cs:7`: "library" ifadesini çözülebilir gerçek host davranışıyla hizalamak (Exe üretimi sekteye uğratılmaz).
- Devralınan görevlerin bulgu kabul koşullarını kendi yeniden üretilebilir evidence'ıyla kapatmak; `NotApplicable`
  kapanış görevlerinin acceptance odağını sahipsiz bırakmamak.

## Out of scope

- Owned surface dışındaki product, migration, project, lock, plan veya başka task evidence dosyası değiştirmek.
- `V1-IAM-008` yüzeyi (`RoleManagementService.cs`) ve onun bulgusu (CODE-008).
- V0/GOV gate kapanış kanıtı üretmek veya C52 admission tuple'ına ek görev eklemek.

## Dependencies

- V0-GOV-035
- V0-GOV-037
- V1-FND-011
- V1-FND-012
- V1-FND-021
- V1-IAM-009
- V1-IAM-003
- V1-CAT-001
- V1-SEC-003

## Deliverables

- Konsolide implementation diff seti, focused tests, PostgreSQL forward/down migration kanıtı ve raw transcript.

## Acceptance evidence

- Bulguya özgü her invariant production ve test katmanında fail-closed doğrulanır; negatif yollar kanıtlanmazsa
  `VERIFIED` verilmez.
- Migration varsa forward/down/forward kanıtı; `database/MigrationComposition/order.json` manifest testleri exit code
  `0` verir.
- `python -B tools/plan-audit/plan_audit_tool.py validate` exit code `0` verir; repository write-set yalnız Owned
  surface ve `evidence/V1-RMD-001/**` altındadır.
- Tüm bulgular kanıtlanana kadar görev `InProgress` kalır; her invariant production ve testlerde fail-closed
  doğrulanır.

## Handoff

- V0-GOV-045
- V0-GOV-048
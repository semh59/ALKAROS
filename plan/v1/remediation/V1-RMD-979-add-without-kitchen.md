# V1-RMD-979 - Mutfaga gondermeden hesaba kalem ekleme (kasiyer)

- Task ID: V1-RMD-979
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer, garsonun girmediği ama müşterinin yediği bir ürünü mutfak fişi açmadan açık hesaba ekleyebilir: kalemde "kasada eklendi" işareti, ekleyen kullanıcı ve zorunlu neden kodu saklanır, stok düşer, denetim kaydı yazılır; ayrı izin (orders.add-without-kitchen) Kasiyer ve üstüne varsayılan verilir, izni olmayan 403 alır.

## Owned surface

- `evidence/V1-RMD-979/**`
- `database/migrations/V1/V1-RMD-979/229-order-item-without-kitchen.up.sql`
- `database/migrations/V1/V1-RMD-979/229-order-item-without-kitchen.down.sql`
- `src/Host/Experience/Orders/KitchenlessAdd/KitchenlessAddStore.cs`
- `src/Host/Experience/Orders/KitchenlessAdd/KitchenlessAddContracts.cs`
- `src/Host/Experience/Orders/KitchenlessAdd/KitchenlessAddRegistration.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - database/MigrationComposition/order.json
  - src/Host/Composition/Migrations/MigrationManifest.cs
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  - tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs
  - src/Modules/Orders/OrderAggregate/OrderItem.cs
  - src/Modules/Orders/OrderAggregate/Order.cs
  - src/Modules/Orders/OrderAggregate/PostgresOrderRepository.cs
  - src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs
  - tests/Modules/Identity/Authorization/Catalog/ApplicationPermissionsTests.cs
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs
  - src/Host/Experience/Orders/OrderManagementContracts.cs
  - src/Host/Experience/Orders/OrderDtoAssembler.cs
  - src/Host/Experience/Orders/OrderCancellation/OrderCancellationRegistration.cs
  - tests/Host/Experience/Billing/KitchenlessAddHttpTests.cs
  - tests/Host/Experience/Billing/BillingSplitTestDatabase.cs
  - tests/Host/Experience/Billing/ALKAROS.Host.Experience.Billing.Tests.csproj
  - tests/Architecture/ApiConventions/idempotency_allowlist.json
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test, düzeltme, mutasyon
2. İki bağımsız qa incelemesi

## Out of scope

- Listede olmayan ekranlar ve dosyalar

## Dependencies

- V1-RMD-978

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

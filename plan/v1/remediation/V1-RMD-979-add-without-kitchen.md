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
- `database/migrations/V1/V1-RMD-979/230-add-without-kitchen-permission.up.sql`
- `database/migrations/V1/V1-RMD-979/230-add-without-kitchen-permission.down.sql`
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
  - tests/Host/Experience/NfcOrdering/ALKAROS.Host.Experience.NfcOrdering.Tests.csproj
  - tests/Host/Experience/OnlineOrdering/ALKAROS.Host.Experience.OnlineOrdering.Tests.csproj
  - tests/Host/Experience/Orders/Comp/ALKAROS.Host.Experience.Orders.Comp.Tests.csproj
  - tests/Host/Experience/Orders/Confirmation/ALKAROS.Host.Experience.Orders.Confirmation.Tests.csproj
  - tests/Host/Experience/Orders/TableDraft/ALKAROS.Host.Experience.Orders.TableDraft.Tests.csproj
  - tests/Host/Experience/Orders/Void/ALKAROS.Host.Experience.Orders.Void.Tests.csproj
  - tests/Host/Experience/Orders/VoidSent/ALKAROS.Host.Experience.Orders.VoidSent.Tests.csproj
  - tests/Host/Experience/QrOrdering/ALKAROS.Host.Experience.QrOrdering.Tests.csproj
  - tests/Modules/Billing/Adjustments/ALKAROS.Billing.Adjustments.Tests.csproj
  - tests/Modules/Billing/BillFoundation/ALKAROS.Billing.BillFoundation.Tests.csproj
  - tests/Modules/Billing/PaymentClosure/ALKAROS.Billing.PaymentClosure.Tests.csproj
  - tests/Modules/Billing/SplitDesign/ALKAROS.Billing.SplitDesign.Tests.csproj
  - tests/Modules/Cash/SessionLifecycle/ALKAROS.Cash.SessionLifecycle.Tests.csproj
  - tests/Modules/Cash/TenderHandler/ALKAROS.Cash.TenderHandler.Tests.csproj
  - tests/Modules/Cash/TransactionLedger/ALKAROS.Cash.TransactionLedger.Tests.csproj
  - tests/Modules/CustomerAccounts/BillCharges/ALKAROS.CustomerAccounts.BillCharges.Tests.csproj
  - tests/Modules/Kitchen/PhysicalPrintRecovery/ALKAROS.Kitchen.PhysicalPrintRecovery.Tests.csproj
  - tests/Modules/Kitchen/PrintQueue/ALKAROS.Kitchen.PrintQueue.Tests.csproj
  - tests/Modules/Kitchen/TicketLifecycle/ALKAROS.Kitchen.TicketLifecycle.Tests.csproj
  - tests/Modules/Orders/ItemExceptions/ALKAROS.Orders.ItemExceptions.Tests.csproj
  - tests/Modules/Orders/OrderAggregate/ALKAROS.Orders.OrderAggregate.Tests.csproj
  - tests/Modules/Orders/SubmitOrder/ALKAROS.Orders.SubmitOrder.Tests.csproj
  - tests/Modules/Payments/Allocations/Persistence/ALKAROS.Payments.Allocations.Persistence.Tests.csproj
  - tests/Modules/Payments/Allocations/RefundIntents/ALKAROS.Payments.Allocations.RefundIntents.Tests.csproj
  - tests/Modules/Payments/CardSettlement/ALKAROS.Payments.CardSettlement.Tests.csproj
  - tests/Modules/Payments/EftTender/ALKAROS.Payments.EftTender.Tests.csproj
  - tests/Modules/Payments/PaymentAggregate/ALKAROS.Payments.PaymentAggregate.Tests.csproj
  - tests/Modules/Payments/TenderComposition/ALKAROS.Payments.TenderComposition.Tests.csproj
  - tests/Modules/QrOrdering/PendingOrders/ALKAROS.QrOrdering.PendingOrders.Tests.csproj
  - tests/Modules/Reconciliation/Payments/ALKAROS.Reconciliation.Payments.Tests.csproj
  - tests/Modules/Reporting/Payments/ALKAROS.Reporting.Payments.Tests.csproj
  - tests/Modules/Tables/CurrentPointers/ALKAROS.Tables.CurrentPointers.Tests.csproj
  - tests/Modules/Tables/PaymentTopology/ALKAROS.Tables.PaymentTopology.Tests.csproj
  - tests/Modules/Tables/Reservations/ALKAROS.Tables.Reservations.Tests.csproj
  - tests/Modules/Tables/TableMerge/ALKAROS.Tables.TableMerge.Tests.csproj
  - tests/Modules/Tables/TableTransfer/ALKAROS.Tables.TableTransfer.Tests.csproj
  - tests/Host/Experience/Billing/BillingSplitTestDatabase.cs
  - tests/Host/MigrationComposition/OrdersRollbackTests.cs
  - tests/Modules/Identity/Authorization/Catalog/PermissionSplitDatabase.cs
  - src/Host/DualScreen/DualScreenApplication.cs
  - tests/Architecture/ApiConventions/idempotency_allowlist.json
  - tests/Architecture/ApiConventions/api_convention_allowlist.json
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

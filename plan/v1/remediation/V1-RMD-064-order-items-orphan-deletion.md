# V1-RMD-064 - Order items orphan deletion

- Task ID: V1-RMD-064
- Status: Done
- Assignee: 0e4d2094-eff5-490e-9402-056e17c86376
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-31

## Goal

`PostgresOrderRepository.SaveAsync` içine silinen/değişen taslak kalemlerini temizleyen orphan-cleanup mantığı eklemek ve `OrderManagementStore.CreateOrUpdateTableDraftAsync` taslak sipariş güncellemelerinde mükerrer satır birikmesini engellemek.

## Owned surface

- `plan/v1/remediation/V1-RMD-064-order-items-orphan-deletion.md`
- `src/Modules/Orders/OrderAggregate/**`
- `src/Modules/Orders/SubmitOrder/**`
- `src/Host/Experience/Orders/OrderManagementStore.cs`
- `tests/Modules/Orders/OrderAggregate/**`
- `tests/Modules/Orders/SubmitOrder/**`
- PO:2026-09-01 kararıyla tests/Host/Experience/Orders/** yüzeyi V1-RMD-083’e devredildi; bu historical task closed kalır.

## In scope

- `PostgresOrderRepository.SaveAsync` içerisinde `knownItemIds.Except(currentItemIds)` hesaplayarak kaldırılan kalemleri `orders.order_items` tablosundan silmek.
- `OrderManagementStore.CreateOrUpdateTableDraftAsync` içerisinde mevcut taslak güncellemelerinde kalem bütünlüğünü doğrulamak.
- Mükerrer kalem eklenmediğini doğrulayan birim/entegrasyon testi eklemek.

## Out of scope

- Diğer sipariş veya ödeme modüllerini değiştirmek.

## Dependencies

- V1-RMD-063

## Deliverables

- Düzeltilmiş `PostgresOrderRepository.cs`, `OrderManagementStore.cs` ve doğrulama testleri.

## Acceptance evidence

- `dotnet test tests/Modules/Orders/SubmitOrder/ALKAROS.Orders.SubmitOrder.Tests.csproj` 0 hata verir.
- Masa taslağı güncellemelerinde mükerrer satır kalmadığı testle kanıtlanır.

## Handoff

- V1-RMD-065

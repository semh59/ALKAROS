# V1-RMD-050 - Order endpoint and dualscreen unification

- Task ID: V1-RMD-050
- Status: Done
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

İkinci, zayıf ve yetkisiz `OrderManagement` rotasını kaldırmak; tüm masa ve sipariş yönetimini `DualScreenApplication.cs` ve `DualScreenStore.cs` içindeki yetkili, session doğrulamalı, katalogdan fiyat çözen, transaction kilitli ve mutfak dispatcher'lı tekil sipariş altyapısına bağlamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-050-order-endpoint-and-dualscreen-unification.md`
- `src/Host/Experience/Orders/OrderManagementContracts.cs`
- PO:2026-08-29 kararıyla src/Host/Experience/Orders/OrderManagementEndpoints.cs, src/Host/DualScreen/DualScreenApplication.cs ve src/Host/Experience/Orders/OrderManagementStore.cs yüzeyleri V1-RMD-054'e devredildi; bu historical task closed kalır.
- PO:2026-08-31 kararıyla src/Host/DualScreen/DualScreenStore.cs yüzeyi V1-RMD-077'ye devredildi; bu historical task closed kalır.
- `evidence/V1-RMD-050/**`

## In scope

- `OrderManagementEndpoints.cs` ve `OrderManagementStore.cs` yapısını `DualScreenStore` ve `SubmitOrderHandler` ile tekleştirmek; yetkisiz endpoint kalıntısını kaldırmak.
- Masa siparişi açma, ürün ekleme ve sipariş submit akışlarında session authentication (`RequireCashierPermissionAsync`), catalog price lookup ve kitchen ticket dispatch güvencelerini sağlamak.

## Out of scope

- PosTerminal istemci kodlarını değiştirmek.

## Dependencies

- V1-RMD-049

## Deliverables

- Yetkili, güvenli ve mutfak kuyruğuna bağlı tekleştirilmiş sipariş host mimarisi.

## Acceptance evidence

- Sipariş açma ve submit işlemlerinde yetkisiz isteklerin 401/403 ile reddedildiği, geçerli isteklerin mutfak ticket'ı oluşturduğu doğrulanır.

## Handoff

- V1-RMD-051

# V1-RMD-054 - Production composition and order billing routing

- Task ID: V1-RMD-054
- Status: Done
- Assignee: 1dec2ab0-b9bc-4bc0-84d5-c4cabf3e4a6a
- Work type: integration
- Surface state: Planned

## Source basis

- PO:2026-08-29

## Goal

`DualScreenApplication.cs` içine `OrderManagement` ve `BillingSplit` servislerini ve route gruplarını bağlamak; `OrderManagementStore.cs` üzerinde katalog fiyat çözümlemesini, masa pointer güncellemesini tekil PostgreSQL transaction altına almak ve yetkilendirmeyi güvenceye bağlamak.

## Owned surface

- `plan/v1/remediation/V1-RMD-054-production-composition-and-order-billing-routing.md`
- PO:2026-08-31 kararıyla src/Host/DualScreen/DualScreenApplication.cs yüzeyi V1-RMD-077'ye devredildi; bu historical task closed kalır.
- PO:2026-08-31 kararıyla src/Host/Experience/Orders/OrderManagementStore.cs yüzeyi V1-RMD-064'e, src/Host/Experience/Orders/OrderManagementEndpoints.cs yüzeyi V1-RMD-066'ya devredildi; bu historical task closed kalır.
- `src/Host/Experience/Billing/BillingSplitApplication.cs`
- `src/Host/Experience/Billing/BillingSplitContracts.cs`
- `evidence/V1-RMD-054/**`

## In scope

- `DualScreenApplication.cs` içinde `AddOrderManagementExperience()`, `AddBillingSplitExperience()`, `MapOrderManagementApi()` ve `MapBillingSplitApi()` çağrılarını eklemek.
- `OrderManagementStore.cs` içinde taslak oluşturma ve güncellemede catalog price çözümlemesini ve table pointer güncellemesini atomik transaction altında yürütmek.
- `OrderManagementEndpoints.cs` üzerinde session ve cashier yetkilendirmesini zorunlu kılmak.

## Out of scope

- PosTerminal React bileşenlerini değiştirmek.

## Dependencies

- V1-GOV-032

## Deliverables

- Üretim hostuna tam entegre, yetkili ve transaction kilitli sipariş/adisyon rotaları.

## Acceptance evidence

- `dotnet build ALKAROS.slnx` 0 hata ve 0 uyarı verir.
- Rota entegrasyonu ve yetkilendirme doğrulanır.

## Handoff

- V1-RMD-055

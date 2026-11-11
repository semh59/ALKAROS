# V1-RMD-931 - Stok takibi olmayan ürün stoksuz satılabilsin

- Task ID: V1-RMD-931
- Status: InProgress
- Assignee: claude-code-session_019vXeYYn5EiQaDrvjG9cpob
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi (G14, bulgu 1) ve Semih kararı (2026-11-11): yeni bir ürün stok eşlemesi olmadığı için satılamıyordu. Karar: ürün formunda "Stok takibi yok" seçeneği olur; bu modda (Untracked) ve hiç stok eşlemesi olmayan ürün, hiç stok girilmeden her kanalda (kasa, garson, QR, NFC, online) satılır ve stok düşümüne, rezervasyona, eksik-eşleme reddine takılmaz. Takipli ürünlerin mevcut kuralları aynı kalır; Untracked ama stok eşlemesi olan ürün eskisi gibi düşer (eşleme varsa takip edilir).

## Owned surface

- `evidence/V1-RMD-931/**`
- `src/Host/Experience/Orders/OrderStockConsumption/UntrackedProducts.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Experience/Orders/OrderStockConsumption/OrderStockConsumptionService.cs
  - src/Modules/Inventory/CrossChannelReservation/PostgresCrossChannelPortionArbiter.cs
  - src/Modules/Inventory/CrossChannelReservation/CrossChannelReservationModels.cs
  - src/Host/Experience/Orders/PendingOrderConfirmation/PendingOrderConfirmationStore.cs
  - src/Host/Experience/OnlineOrdering/YemeksepetiOrderIntakeService.cs
  - tests/Host/Experience/Orders/Confirmation/QrConfirmationReservationHttpTests.cs
  - tests/Host/Experience/Orders/Confirmation/OrderManagementConfirmationTestDatabase.cs
  - tests/Host/Experience/OnlineOrdering/YemeksepetiOrderIntakeTests.cs
  - tests/Host/Experience/OnlineOrdering/OnlineOrderingTestDatabase.cs
  - src/Clients/PosTerminal/src/strings.ts
  - src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.test.tsx
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftHttpTests.cs
  - tests/Host/Experience/Orders/TableDraft/OrderManagementTableDraftTestDatabase.cs
  - tests/Modules/Inventory/CrossChannelReservation/CrossChannelPortionArbiterTests.cs
  - tests/Modules/Inventory/CrossChannelReservation/CrossChannelReservationTestDatabase.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı testler: ürün formu Stok takibi yok seçeneği; sipariş gönderiminde eşlemesiz Untracked ürün satılır, eşlemesiz takipli ürün hâlâ reddedilir; çapraz kanal rezervasyonu eşlemesiz Untracked satırı atlar
2. Düzeltme, mutasyon
3. İki bağımsız qa incelemesi

## Out of scope

- Mevcut ürünün stok modunu sonradan değiştirme ekranı

## Dependencies

- V1-RMD-930

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

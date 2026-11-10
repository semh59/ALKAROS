# V1-RMD-920 - Hızlı satış gönderiminde stok hataları 500 yerine Türkçe 409 döner

- Task ID: V1-RMD-920
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi (G14, rapor 12): yeni ürünle `POST /api/v1/terminals/{terminal}/orders/{order}/submit` 500 "İşlem tamamlanamadı." döner; sunucu günlüğünde `ProductStockNotConfiguredException`, `StockItemHasNoDefaultLocationException`, `InsufficientOrderStockException`. Neden: bu üç iş kuralı hatası yalnız Siparişler alanının eşlemesinde tanımlı; terminal rotası ortak varsayılan eşlemeye düşüyor. Karar: üç hata varsayılan eşlemede de 409 olur (kodlar ve Türkçe mesajlar Siparişler alanıyla aynı, tek kaynaktan: istisna sınıflarındaki `UserMessage`), kasiyer sebebi ("'X' için stok tanımlanmamış, lütfen yöneticiye bildirin" gibi) görür.

## Owned surface

- `evidence/V1-RMD-920/**`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/Composition/Errors/ApiErrorCatalog.cs
  - src/Host/Experience/Orders/OrderStockConsumption/OrderStockConsumptionService.cs
  - src/Host/Experience/Orders/OrderManagementEndpoints.cs
  - tests/Host/Errors/ApiErrorCatalogTests.cs
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı test: varsayılan eşleme bu üç hata için 409 ve Türkçe mesaj verir
2. Mesajların istisna sınıflarında tek kaynağa alınması, iki eşlemenin onu kullanması
3. Mutasyon ve iki bağımsız qa incelemesi

## Out of scope

- Stoksuz ürün satılabilsin mi kararı (ürün kararı, ayrı soru)

## Dependencies

- V1-RMD-919

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

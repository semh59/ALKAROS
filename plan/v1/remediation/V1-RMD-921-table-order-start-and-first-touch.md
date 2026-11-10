# V1-RMD-921 - Masa siparişi: devam etme, oturmuş masada sipariş açma ve ilk ürün dokunuşu

- Task ID: V1-RMD-921
- Status: Done
- Assignee: claude-code-session_019vXeYXYQSTiLW1J4UDYBY6
- Work type: implementation
- Surface state: Existing

## Goal

Kasiyer denemesi (G14, rapor 12, bulgu 2). Kodda doğrulanan üç neden: (1) Sunucu: terminalin kendi etkin masa siparişi (Taslak) varken aynı masa için "Siparişe devam et" `POST /orders/table` "Terminal already has an active order" ile 409 döner; oysa kendi masasının taslağına yeniden bağlanmak geçerli bir istek (mevcut masa-siparişi yolu zaten bunu yapıyor ama ondan önceki denetim engelliyor). (2) Sunucu ve istemci: "Masayı aç" masayı Dolu yapar ama siparişsiz bırakır; sunucu yalnız Müsait masaya sipariş açar, istemci düğmeyi pasif bırakır: çıkmaz. Karar: siparişsiz ve hesapsız Dolu masaya da sipariş açılır. (3) İstemci: masadan ana kasaya dönünce ekrandaki sipariş bilgisi eski kalır, ilk ürün dokunuşu yeni sipariş açmaya çalışıp 409 alır (ikinci dokunuş çalışır). Karar: ana ekrana dönüldüğünde etkin sipariş yeniden okunur.

## Owned surface

- `evidence/V1-RMD-921/**`
- `src/Clients/PosTerminal/src/routes/tableOrderAvailability.ts`
- `src/Clients/PosTerminal/src/routes/tableOrderAvailability.test.ts`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Host/DualScreen/DualScreenStore.Orders.cs
  - src/Host/DualScreen/DualScreenExceptions.cs
  - src/Host/Composition/Errors/ApiErrorCatalog.cs
  - tests/Host/Errors/ApiErrorCatalogTests.cs
  - tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs
  - src/Clients/PosTerminal/src/routes/Cashier.tsx
  - src/Clients/PosTerminal/src/routes/Cashier.first-touch.test.tsx
  - src/Clients/PosTerminal/src/routes/workspace.tsx
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Kırmızı testler: aynı masaya devam (sunucu), siparişsiz Dolu masaya sipariş (sunucu), dönüşte etkin siparişin yeniden okunması (istemci), masa uygunluk kuralı (istemci)
2. Düzeltmeler ve mutasyon, iki bağımsız qa
3. qa bulguları: birleştirilmiş (ikincil) masaya sipariş açılmaz; yeniden bağlanma yalnız masa göstergesi terminalin taslağını gösteriyorsa; terminal başka taslak tutarken 409 kullanıcıya anlaşılır Türkçe sebebi söyler; dönüşte yeniden okumanın eski yanıtı ezmemesi ve ilk dokunuşun 409 durumunda etkin siparişle devam etmesi

## Out of scope

- Masadan hesap alma yolu ve sepet başlığında masa adı (V1-RMD-922)

## Dependencies

- V1-RMD-920

## Acceptance evidence

1. Test çıktıları, mutant listesi ve sonuç gerekçesi
2. İki ardışık bağımsız qa-* incelemesi ve doğrulama turu.

## Handoff

- None

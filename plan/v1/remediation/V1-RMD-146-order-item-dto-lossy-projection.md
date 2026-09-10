# V1-RMD-146 - OrderItemDto siparişin yarısını düşürüyor

- Task ID: V1-RMD-146
- Status: Done
- Assignee: Claude Opus 5
- Work type: implementation
- Surface state: Existing

## Goal

Garson arayüzü tasarlanırken (2026-09-10) bulundu: garsonun sipariş okuduğu
tek sözleşme olan `OrderItemDto` sekiz alan taşıyor ve ekranın ihtiyaç
duyduğu dördü orada yok. Domain hepsine sahip; kaybı iki eşleme yapıyor
(`OrderManagementStore.MapToDto`, `NfcOrderingStore.MapToDto`). En sert
sonucu `(int)i.Quantity` kırpması: V1-RMD-144'ten sonra bir garson yarım
porsiyon gönderebilse bile geri okurken miktar **0** görünür. Ayrıca kalemin
mutfak durumu, iptal edilip edilmediği ve ne zaman girildiği hiç dönmüyor —
bu yüzden kalem bazında mutfak durumu ve "aynı turu tekrarla" bugünkü
sözleşmeyle yazılamıyor.

## Owned surface

- `plan/v1/remediation/V1-RMD-146-order-item-dto-lossy-projection.md` (yeni)
- Sınırlı ek — aşağıdaki yollar ilgili görevlerin sahipliğinde kalır (yollar
  geri-tik olmadan yazıldı ki denetleyici bunları sahiplik iddiası olarak
  parse etmesin):
  - src/Host/Experience/Orders/OrderManagementContracts.cs (Orders Management
    sahipliğinde) — `OrderItemDto` ve `OrderItemDraftDto` alanları.
  - src/Host/Experience/Orders/OrderManagementStore.cs (Orders Management
    sahipliğinde) — yalnız `MapToDto` eşlemesi.
  - src/Host/Experience/NfcOrdering/NfcOrderingStore.cs (V12-NFC-00x
    sahipliğinde) — yalnız aynı okuma eşlemesi.
    `NfcOrderingContracts.cs`'e dokunulmadı: aşağıdaki gerekçeyle müşteri
    kanalının YAZMA tarafı tam sayı kalıyor.
  - tests/Host/Experience/Orders/TableDraft/** (V1-RMD-113/123 sahipliğinde)
    — yeni alanların ve kesirli miktarın uçtan uca korunduğu testler.

## In scope

1. **Miktar kesirli kalıyor.** `OrderItemDto.Quantity` (okuma, her iki
   kanal) ve `OrderItemDraftDto.Quantity` (garson/kasa yazma) `int` yerine
   `decimal`; iki eşlemedeki `(int)i.Quantity` kırpması kalkıyor.
   `orders.order_items.quantity` zaten `NUMERIC(18,3)`, `OrderItem.Quantity`
   zaten `decimal`, kasa hattının `AddOrderItemRequest`'i zaten `decimal` —
   kırpma yalnız bu iki eşlemedeydi.

   `NfcOrderItemRequestDto.Quantity` kasıtlı olarak `int` bırakıldı. Daha
   önce "üç `int`" diye raporlanmıştı; uygulama sırasında bunun yalnız ikisi
   olduğu görüldü. Müşterinin kendi telefonundan gönderdiği istek yarım
   porsiyon taşımaz — QR/NFC menüsünde öyle bir seçenek yok ve müşteri
   kanalının kesirli miktar göndermesi için bir sebep de yok. Buna karşılık
   okuma tarafı artık `decimal`, yani garsonun girdiği yarım porsiyon
   müşterinin ekranında da doğru görünür. Kanalın yazma yüzeyini gereksiz
   yere genişletmek, doğrulanması gereken bir giriş daha açardı.
2. **Alt sınır.** `decimal`e açılan yazma yüzeyi, şemanın üç ondalığına
   yuvarlanınca sıfıra düşecek bir miktarı (ör. `0.0001`) artık sessizce
   kabul edemez: taslak oluşturma `0.001`'in altını Türkçe hatayla reddeder.
   Domain'in kendi `quantity <= 0` kuralı yerinde kalır, bu onun önüne
   geçen bir yuvarlama kapısıdır.
3. **Kalemin gerçek durumu dönüyor.** `OrderItemDto` üç alan kazanıyor:
   `Status` (`OrderItemState`), `KitchenState` ve `CreatedAt`. Üçü de
   `OrderItem`'da zaten var. `Status` olmadan iptal edilmiş kalem geçerli
   kalemden ayırt edilemiyor; `KitchenState` olmadan garson hazırlanan ve
   hazır olanı yalnız KDS'nin kendi `/tickets` yüzeyine giderek
   öğrenebiliyor; `CreatedAt` olmadan "son tur" tanımlanamıyor.

## Out of scope

- `OrderItemModifier`'ların hiç dönmemesi ve sipariş hattının onları
  düşürmesi — ayrı, daha büyük bir iş (katalog tarafı da eksik).
- Garson/kasa arayüzlerinin bu yeni alanları kullanması: ekranlar Faz 1'de
  baştan yazılıyor, bu görev yalnız sözleşmeyi doğru hâle getirir.
- Kesirli miktarın müşteri kanalında (QR/NFC) arayüzden seçilebilmesi;
  sözleşme onu taşıyor ama müşteri ekranında yarım porsiyon seçeneği yok.

## Dependencies

- V1-RMD-144

## Acceptance evidence

- `dotnet build ALKAROS.slnx`: 0 Uyarı, 0 Hata.
- Gerçek Postgres'e karşı (`alkaros-test-pg`, port 55432), ayrı ayrı,
  gerçek çıkış koduyla:
  - `ALKAROS.Host.Experience.Orders.TableDraft.Tests`: **22/22** (19'dan).
    Üç yeni senaryo: yarım porsiyon taslak oluşturma, gönderme ve geri okuma
    boyunca `0,5` kalıyor (düzeltme öncesi `0` dönerdi); `0,0001` gibi
    yuvarlanınca sıfıra düşecek bir miktar 400 ile reddediliyor; gönderilen
    kalem `Status` (`Draft` → `Active`), `KitchenState` (`NotSent`) ve
    gerçek bir `CreatedAt` taşıyor.
  - `ALKAROS.Host.Experience.NfcOrdering.Tests`: 17/17 — aynı okuma eşlemesi
    değiştiği hâlde müşteri kanalı etkilenmedi.
  - `ALKAROS.Host.Experience.Orders.Confirmation.Tests`: 17/17,
    `...VoidSent.Tests`: 12/12, `...Comp.Tests`: 9/9, `...Void.Tests`: 5/5.
- Migration yok — şema zaten `NUMERIC(18,3)`.
- Semih'in elle deneyebileceği senaryo: garson PWA'dan bir ürünü yarım
  porsiyon olarak gönder (V1-RMD-144'ün stok düşümüyle birlikte), sonra
  `GET /api/v1/terminals/{terminalId}/orders/{orderId}` çağır: `quantity`
  alanı `0.5` dönmeli (bugün `0` dönüyor) ve kalem `status`,
  `kitchenState`, `createdAt` alanlarını taşımalı.
- `python tools/plan-audit/plan_audit_tool.py validate`: 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py`: 1 önceden var olan
  ihlal (`InventoryAdjustmentService.cs:96`, bu görevden bağımsız), yeni
  ihlal yok.

## Handoff

- None

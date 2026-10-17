# V1-RMD-580 - Trendyol Go belge uyumu düzeltmeleri

- Task ID: V1-RMD-580
- Status: Done
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-17

## Goal

Yayımlanmış Trendyol Go belgesiyle (developers.tgoapps.com, 2026-10-17 okundu) kodun üç çelişkisi giderilir:
1. Sipariş ara toplamı `totalPrice − totalDeliveryPrice` diye hesaplanıyordu; belgedeki örnekte satırların toplamı (290+150+162,70) `totalPrice` ile aynıdır, teslimat ücreti ayrıdır.
2. Fiyat güncellemesi `restaurantId` göndermiyordu; belgeye göre gönderilmezse fiyat satıcının tüm restoranlarına uygulanır.
3. Webhook olay türü listesi belgede olmayan `storeChanged` ve `sellerChanged` değerlerini kabul ediyordu.

## Owned surface

- `plan/v1/remediation/V1-RMD-580-trendyol-go-doc-conformance.md`
- `evidence/V1-RMD-580/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/OnlineOrdering/Providers/TrendyolGo/OrderIntake/** - yalnız ara toplam hesabı ve olay türü listesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/OnlineOrdering/Providers/TrendyolGo/Menu/** - yalnız fiyat güncelleme gövdesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/OnlineOrdering/** - yalnız ilgili testler
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Ara toplam: indirim yoksa `totalPrice` olduğu gibi karşılaştırılır.
- Fiyat güncellemesinde mağaza kimliği sayısal değilse istek gönderilmez (hata); sayısal ise her kalemde `restaurantId` gönderilir.
- `storeChanged` ve `sellerChanged` webhook yolu olarak kabul edilmez.

## Out of scope

- Gerçek hesapla doğrulama (V12-TGO-001 Blocked); indirim payının saklanması (V1-RMD-474).

## Dependencies

- V12-TGO-002
- V12-TGO-004

## Acceptance evidence

1. Her düzeltme için test; mutasyon kanıtı `evidence/V1-RMD-580/` altındadır.
2. Çıkarılan olay türleri artık yol olarak reddedilir.

## Handoff

- None

# V1-RMD-580 - Trendyol Go belge uyumu düzeltmeleri

Kaynak: developers.tgoapps.com belge sayfaları (2026-10-17 okundu).

Düzeltmeler
1. Ara toplam: `totalPrice` satır toplamıdır, teslimat ücreti (`totalDeliveryPrice`) ayrıdır (belge örneği: 290+150+162,70 = 602,70 = totalPrice, teslimat 15,99 ayrı). Kod eskiden teslimat ücretini çıkarıyordu; kendi kuryeli siparişte toplam eksik hesaplanıyordu.
2. Fiyat güncellemesi her kalemde `restaurantId` gönderir. Belge: gönderilmezse fiyat satıcının tüm restoranlarına uygulanır. Mağaza kimliği sayısal değilse istek hiç gönderilmez.
3. `storeChanged` ve `sellerChanged` belgede artık yok; webhook yolu olarak kabul edilmez (404 UNKNOWN_EVENT_TYPE).

Kanıt: `test-runs.log` (Trendyol Go testleri 49/49, gerçek Postgres), `mutations.log` (4/4 mutant öldürüldü).

Sınırlar (doğrulanamadı)
- Gerçek Trendyol Go hesabıyla denenmedi (V12-TGO-001 Blocked).
- `restaurantId` değerinin mağaza kimliğiyle aynı olduğu varsayıldı (belge: restaurantId getRestaurants servisinden alınır); gerçek hesapta doğrulanmalı.
- Belgedeki istek sınırı iki farklı değerle geçiyor; menü istemcisine sınırlayıcı eklenmedi.

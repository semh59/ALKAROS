# V1-RMD-979 sonuc

Eklenen: POST /api/v1/terminals/{terminalId}/orders/{orderId}/items/without-kitchen (izin orders.add-without-kitchen; Kasiyer, Supervizor, Mudur; garson yok).
- Kalem Active kaydedilir, mutfak durumu Served (yenmis; sade iptal yollari kapali), mutfak fisi yazilmaz; stok dusulur; ekleyen kullanici + neden kodu (waiter-forgot, customer-asked, other) kalemde saklanir; denetim kaydi order.item-added-without-kitchen ayni islemde yazilir.
- Hesabi olan veya kapanmis siparise ekleme 409; bilinmeyen neden 400; ayni anahtarla tekrar tek kalem.
- Migration 229 (kalem sutunlari + CHECK), 230 (izin ve roller). Siparis gorunumunde (OrderItemDto) addedWithoutKitchenReason alani.

Testler: KitchenlessAddHttpTests 20 (gercek PostgreSQL), izin katalogu 261, migration/manifest, OrderAggregate 187, ApiConventions 17, 36 test projesinde regresyon (regression.txt). Mutasyonlar mutations.txt.
Mutasyonlar: mutations.txt (M2 M4 M6 M1 M5 olu; M3 esdeger: bilinmeyen neden veritabani CHECK kuralinda da reddediliyor, sonuc ayni).

Kapsam disi, ayri gorevlerde: hesap kalemlerinde (bill_items) ve raporda "kasada eklendi" ayri sayimi; kasa ekraninda dugme ve neden secimi.

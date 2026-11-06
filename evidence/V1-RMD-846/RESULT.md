# V1-RMD-846 sonuç

`POST /api/v1/terminals/{terminalId}/billing/bills/merge` (gövde: `orderIds`, `idempotencyKey`) aynı masadaki en az iki ve en çok yirmi açık siparişi tek hesapta toplar (`BillSourceOperations.CreateMergedBill`, V1-RMD-845'in kalem yoluyla siparişine bağlanır). Yetki `bills.split` (`RequireMutationAsync`, uç gövdesinden önce); hesap çağıran terminale sahiplendirilir (V1-RMD-843) ve masa işaretçisi aynı işlemde bağlanır. Ödenince V1-RMD-845'in kapatma akışı bütün kaynak siparişleri tamamlar (uçtan uca test).

## Kurallar (hepsi gerçek Postgres ve gerçek host ile test edildi)
- Reddedilenler Türkçe kodlarla: `MERGE_NEEDS_TWO_ORDERS`, `MERGE_TOO_MANY_ORDERS`, `MERGE_DIFFERENT_TABLES`, `MERGE_ORDER_SOURCE` (yalnız kasa ve garson siparişleri; QR/çevrimiçi dışarıda, kanal raporu iadesi sipariş bazlı olduğundan), 404 bulunamayan sipariş, `UNSUPPORTED_ORDER_STATE` (taslak, iptal, tamamlanmış, onay bekleyen), `MERGE_ORDER_ALREADY_BILLED`, `MERGE_MIXED_SENT` (bir kısmı kasaya gönderilmiş), `MERGE_ORDER_EMPTY` (faturalanacak kalemi olmayan sipariş; gönderilmemiş tur kalemleri tek siparişli hesap gibi dışarıda kalır), `IDEMPOTENCY_KEY_REQUIRED`, `IDEMPOTENCY_KEY_REUSED`.
- İdempotency: hesap numarası `BILL-M-` + (terminal + anahtar) özeti. Aynı terminal, aynı anahtar, aynı sipariş kümesi aynı hesabı döndürür; başka küme veya iptal edilmiş hesap `IDEMPOTENCY_KEY_REUSED`; başka terminal aynı anahtarla kendi sonucunu alır (başkasının hesabı dönmez). İki terminalin yarışında kalem kısıtı tek hesap bırakır (biri 200, biri `MERGE_ORDER_ALREADY_BILLED`).
- Kilit: siparişler işlem içinde `FOR UPDATE` (kimlik sırasıyla) kilitlenir; durum, masa, kasaya gönderilmişlik ve satır sayısı yeniden doğrulanır, siparişler kilit altında yeniden okunup kalemler oradan alınır (kilit sırasında eklenen ya da iptal edilen kalem yanlış hesaba girmez).

## Doğrulama
- Kırmızı `red.log`: uç ve sözleşme yokken testler derlenmiyor (özellik yok).
- Yeşil: yeni 10 test + oturum/yetki tablosu (`green-new.log`); Billing projesi, mimari sınır ve API sözleşme testleri tam (`final-*.log`).
- Mutasyon (`mutations.log`, `cmp` ile geri alındı): iki sipariş kuralı, tekrar yolu, anahtar farklı küme, iptal edilmiş hesap anahtarı, masa, kaynak, durum, hesabı zaten var, karışık gönderim (kilit altında), kalemsiz sipariş (kilit altında), kilit adımı, üst sınır, anahtarın terminali içermesi, taslak tur kalemi, masa işaretçisi, yarış kurtarması, hesap sahiplenme, yetki: testler düşüyor.
- Düşürülemeyen: kilit altındaki yeniden okumanın kendisi (kilit sırasında araya giren iptal veya kalem ekleme) için belirli sıralı bir çakışma testi yazılamadı; kod incelemesiyle doğrulandı.

## İnceleme
- qa-api-uclar: üst sınır, kalemsiz sipariş, anahtar kapsamı (terminal), kod hizası (`IDEMPOTENCY_KEY_REUSED`), sipariş satırı kilidi altında yeniden doğrulama, test boşlukları: hepsi uygulandı.
- qa-guvenlik-yetki: kilit altında kalemlerin yeniden okunması ve satır sayısı denetimi uygulandı; yetki, claim ve kilit sırası temiz bulundu.
- qa-bulgu-dogrulayici: gönderilmemiş tur (Draft) kalemleri hesaba giriyordu (yüksek): düzeltildi (`IsBillable`), test ve mutasyon eklendi; ayrı bağlantı yerine aynı işlemde okuma. Açık kalan düşük risk: masa birleştirme/kasaya gönderme/geri alma akışları masa satırını önce, birleştirme önce sipariş satırlarını sonra masa satırını kilitler; ters sıra eşzamanlı olursa Postgres 40P01 ile birini düşürür (mevcut `BILL_BUSY` 503 eşlemesiyle tekrar denenebilir, veri bozulmaz).

## Koordinatöre
- Karar: QR ve çevrimiçi siparişler birleştirilemez (kanal raporu iade bölüşümü ürün kararı gerektirir).
- Karar: başka terminalde açık hesabı olan sipariş birleştirilemez (önce o hesap iptal edilir).
- Kasa ekranında yetkisiz kullanıcı için eylemi gizlemek, kuyruk uç noktasının yetki bilgisi taşımasını gerektirir; ekran sunucunun `FORBIDDEN` yanıtını Türkçe gösterir (bkz. V1-RMD-847).

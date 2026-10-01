# V1-RMD-485 - QR siparişinde ekstra seçim ve zorunlu grup denetimi

QR ile sipariş veren müşteri artık ekstra seçebilir; sunucu ad ve fiyatı katalogdan çözer, grup kurallarını (zorunlu, en çok, ürüne ait olma) `QrPendingOrderStore` içinde denetler,
seçilenler `qr-ordering.order-submitted.v1` olayıyla siparişe taşınır. Önceden zorunlu ekstra grubu olan ürün seçimsiz sipariş edilebiliyordu.

## Değişiklikler

- `QrOrderSubmissionContracts.cs`, `QrPendingOrderStore.cs`: istek `Modifiers` taşır; kural ihlali 400 `VALIDATION_FAILED` ve masa ayrılmaz, olay kuyruğa girmez.
- `QrOrderIntegrationEvents.cs`: `QrOrderSubmittedItem.Modifiers` isteğe bağlı (kuyruktaki eski iletiler `null` okunur).
- `QrOrderSubmittedConsumer.cs`: ekstralar sipariş kalemine yazılır (ad ve fiyat farkı olay anındaki katalog değeri).
- Müşteri menüsü (`menu-app.js`): ekstra grubu olan üründe seçici paneli; zorunlu grup tamamlanmadan "Sepete ekle" kapalı; sepet satırı kimliği ürün + ekstralar. Sipariş sayfası (`order-entry.js`) satırda ekstraları gösterir ve yalnız ekstra kimliklerini gönderir.
- **Bulunan ayrı hata:** `GET /api/v1/qr/menu` düz bir dizi döndürüyor, menü sayfası ise `page.items` bekliyordu; müşteri menüsü gerçek sunucuda hiç yüklenmiyor ("Menü şu anda yüklenemedi") ve bu ilk günden beri böyleydi (testler yalnız dosya metnine bakıyordu). Diziyi de kabul edecek şekilde düzeltildi; ayrıca gerçek sayfa kodu jsdom'da çalıştırılarak doğrulandı.

## Kanıt

- `tests-host.log` (QR Host testleri, 3 yeni), `tests-orders.log` (tüketici, 1 yeni), `tests-module.log` (QR modül testleri), CustomerWeb `pytest` (2 yeni sayfa testi).
- `musteri-sayfasi-deneme.log`: sayfa kodu jsdom'da çalıştı: ekstrasız ürün doğrudan sepete, ekstralı ürün panel açar, zorunlu grup tamamlanmadan düğme kapalı, fiyat 100+5+10=115, aynı ürün başka ekstralarla ayrı satır, aynı ekstralarla birleşir, düz dizi menü yanıtı çizilir.
- `mutation.log`: 5 mutant (grup kuralı denetlenmiyor, yabancı ekstra çözülüyor, tüketici ekstraları düşürüyor, panel düğmesi hep açık, düz dizi desteği yok) kırmızı; dosyalar geri alındı, `fc /b`/`cmp` özdeş.
- `gercek-deneme.log`: gerçek Host + Postgres: masa jetonuyla oturum, menüde Steak'in zorunlu "Pişirme" grubu; seçimsiz 400, iki seçenek 400, başka ürünün ekstrası 400, trüf soslu 2 adet 202; outbox sonrası siparişte "Trüf soslu" 25,00 × 2 kaydı (`source=Qr`, `PendingConfirmation`).

## Açık kalan

- Menü uç noktası sayfalı (`X-Next-Cursor` başlığı); müşteri sayfası yalnız ilk sayfayı gösteriyor, ayrı konu.
- Reçete maliyeti ve rapor kırılımı ekstraları sayarken kullanılmıyor.
- Tarayıcıda gerçek telefon denemesi yapılmadı (jsdom + gerçek Host uç noktaları kullanıldı).

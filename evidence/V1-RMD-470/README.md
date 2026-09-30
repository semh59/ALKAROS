# V1-RMD-470 - Online siparişin e-Arşiv fatura taslağı: çekirdek

Bu görev sipariş başına fatura TASLAĞINI saklar. Numara, UBL ve QNB gönderimi yoktur (`V0-QNB-001` engelli); taslak kesilmiş fatura değildir.

## Değişiklikler

- Migration 171 (`database/migrations/V1/V1-RMD-470/`): `invoicing.seller_profile` (tek satır), `invoicing.order_invoices` (`UNIQUE(order_id)`, satıcı anlık görüntüsü,
  internet satışı alanları, `net + tax = gross`, yalnız durum değişebilir), `invoicing.order_invoice_lines` (değişmez). Kayıtlar: `order.json`, `MigrationManifest`, `ManifestTests`.
- `src/Modules/Invoicing/Generation/OrderInvoices/`: `SellerProfile` (+ doğrulama), `PostgresSellerProfileStore`, `OrderInvoiceModels`, `IOrderInvoiceDraftService`,
  `PostgresOrderInvoiceDraftService` (idempotent, eşzamanlıya dayanıklı). `InvoicingGenerationModule` kaydı.
- Kullanılmayan-servis izin listesine iki servisin geçici kaydı (`V1-RMD-471` ve `V1-RMD-472` bağlayacak).

## Plandan sapma

- Plan "oran başına satır, `InvoiceTaxCalculator`" diyordu. Uygulamada satırlar sipariş kalemleridir (müşteri yediklerini görür) ve tutarlar sipariş kaleminde
  saklandığı gibi alınır (kalem başı yuvarlama, sipariş toplamıyla ve raporlarla aynı); böylece fatura toplamı sipariş toplamıyla birebir eşit kalır.
  `InvoiceTaxCalculator` dönemsel hesap faturası için kalır.
- Alıcı her zaman nihai tüketicidir. "Alıcı bilgisi eksik" işareti eşik tutarı doğrulanmadan konmadı (kaynaklarda 500 TL üstü için müşteri bilgisi geçiyor; resmî metin
  doğrulanmadı); `V1-RMD-474` ile müşteri verisi geldiğinde eklenecek.

## Kanıt

- `green-generation.log`: Invoicing.Generation 45/45 (yeni 24 test: KDV dahil satır ve toplam uyumu, satıcı anlık görüntü, tekrar çağrı, 8 eşzamanlı çağrı tek fatura,
  satıcı profili yokken taslak açılmaması, geçersiz sipariş, değişmezlik, veri tabanı toplam kısıtı, profil doğrulama ve depo).
- `mut/mutant-generation.log`: `ON CONFLICT`, tekrar çağrı kısa yolu ve `net + tax = gross` doğrulaması kaldırılınca 3 test kırmızı; dosya geri alındı, `cmp` özdeş.
- `migration-cycle.log`: boş veritabanında tüm migration'lar, 171 down, 171 up, ikinci up (idempotent) exit 0.
- `green-host-migration.log` (55/55), `green-boundaries.log` (9/9): manifest, sayım ve modül sınırları.

## Açık kalan

- Teslimde tetikleme `V1-RMD-472`, satıcı bilgisi ekranı `V1-RMD-471`, liste ekranı `V1-RMD-473`, indirim ve ödeme verisi `V1-RMD-474`.
- Yasal 7 günlük süre için gönderim gelene kadar fiş veya elle fatura gerekir.

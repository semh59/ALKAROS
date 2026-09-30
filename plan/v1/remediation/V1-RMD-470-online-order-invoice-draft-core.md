# V1-RMD-470 - Online siparişin e-Arşiv fatura taslağı: çekirdek

- Task ID: V1-RMD-470
- Status: InProgress
- Assignee: claude-code-session_01Vj7DgrFRRgpSMFfXpfSjwx
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-30

## Goal

Semih 2026-09-30'da online (Yemeksepeti, Trendyol Go) siparişler için faturayı ALKAROS'un kesmesine karar verdi. GİB özelgesi
(08.01.2018, 39044742-KDV.1-19196): restoran, platform kullanıcısı adına hizmetin verildiği tarihten itibaren azami 7 gün içinde
fatura düzenler; platform yalnız komisyon faturası keser. Bugünkü fatura kodu (`V14-INV-002`) hesap defterinden dönemsel
müşteri faturasıdır; sipariş başına çalışmaz ve gönderemez. Bu görev sipariş başına fatura TASLAĞINI üretip saklar; numara, UBL ve
QNB gönderimi yoktur (`V0-QNB-001` engelli). Taslak kesilmiş fatura değildir.

Kurallar: menü fiyatı KDV dahil (`V1-RMD-467`); satırlar sipariş kalemlerinden, oran başına `InvoiceTaxCalculator` ile; alıcı
nihai tüketici (TCKN 11111111111) — müşteri bilgisi şifreli gelen yükte olduğundan ve eşik üstü satış için gerekeceğinden alıcı
alanı boş bırakılabilir ve taslak "alıcı bilgisi eksik" işaretlenir; internet satışı alanları (web adresi, ödeme şekli ve tarihi,
taşıyıcı adı ve VKN, hizmet tarihi); satıcı kimliği (ünvan, VKN, vergi dairesi, adres) fatura anında anlık görüntü olarak kopyalanır.
Restoranın karşıladığı indirim satır indirimi, platformun karşıladığı kısım matrahta kalır (KDV Kanunu md. 20); bu iki alan sipariş
verisinde henüz yoktur (`V1-RMD-474`), o zamana dek taslak yalnız sipariş kalemlerinin gerçek tutarını taşır. Teslimat ücreti bu
faturada değildir (platform kuryesi ise platform keser).

## Owned surface

- `plan/v1/remediation/V1-RMD-470-online-order-invoice-draft-core.md`
- `database/migrations/V1/V1-RMD-470/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Invoicing/Generation/OrderInvoices - yalnız yeni sipariş faturası taslağı kodu
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Invoicing/Generation/OrderInvoiceDraftTests.cs - yalnız yeni davranışın testleri
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json - yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Composition/Migrations/MigrationManifest.cs - yalnız bu görevin migration'ı
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/MigrationComposition/Manifest/ManifestTests.cs - yalnız bu görevin migration'ı

## In scope

- Migration 171: `invoicing.seller_profile` (tek satır), `invoicing.order_invoices` (`UNIQUE(order_id)`, durum `Draft`, satıcı anlık görüntüsü,
  internet satışı alanları, alıcı durumu, tutarlar `net + tax = gross`), `invoicing.order_invoice_lines` (değişmez); `down`.
- Alan nesnesi, oran başına satır üretimi, idempotent `PostgresOrderInvoiceDrafts` (sipariş kimliği ile tekrar çağrı aynı taslağı döner).
- Testler: KDV dahil satır bölme (100 TL, %10 = 9,09), oran karışımı, idempotency, eşzamanlı çağrı, değişmezlik, satıcı profili eksik
  iken taslak açılmaması.

## Out of scope

- Numara serisi, UBL, QNB gönderimi, kuyruk; uç noktalar ve ekran (`V1-RMD-471`, `V1-RMD-473`); teslimde tetikleme (`V1-RMD-472`);
  indirim ve ödeme verisinin yakalanması (`V1-RMD-474`).

## Dependencies

- None

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Postgres denemesi; çıktılar `evidence/V1-RMD-470/` altındadır.

## Handoff

- None

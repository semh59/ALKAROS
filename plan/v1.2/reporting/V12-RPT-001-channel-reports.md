# V12-RPT-001 - Implement channel reports

- Task ID: V12-RPT-001
- Status: Done
- Assignee: Claude Opus 5.5 (session 703155c9)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.20
- PDF:II.10
- PDF:III.31

## Goal

Onaylanan metrik tanımlarından QR ve çevrimiçi kanal hacim, değer, iptal ve mutabakat metriklerini raporlayın.

## Owned surface

- `src/Modules/Reporting/Channels/**`, `tests/Modules/Reporting/Channels/**`
- `src/Host/Experience/Reporting/ChannelReportEndpoints.cs` — sürümlü rapor ucu; bu görevle oluşturulan yeni dosya.
- `evidence/V12-RPT-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan; Semih 2026-09-25 "Sınırlı ek + yol
  notu" kararı):
  - src/Modules/Reporting/ReportingModule.cs — `IChannelReportService` kaydı.
  - src/Host/DualScreen/DualScreenApplication.cs — rapor ucunun kaydı.
  - tests/Host/Experience/Reconciliation/ (V1-RMD-250 sahipliğinde) — yeni ChannelReportHttpTests.cs; yönetici
    oturumu ve izin fikstürü orada.
  - ALKAROS.slnx ve `dotnet restore`'un ürettiği packages.lock.json.
- Metrik tanımı notu: V0-DOM-008 tablosunda "kanal hacmi/değeri" adlı ayrı bir satır yoktur. Bu görev yeni bir
  metrik uydurmaz; kanal raporunu V0-DOM-008'in onaylı kurallarından türetir. Tanımlar aşağıdadır; Semih'in
  onayına açıktır:
  - İş günü: Europe/Istanbul servis günü (kural 3, V0-CMP-002). Sipariş `created_at` ile, sağlayıcı reddi ilk
    webhook zamanıyla, mutabakat vakası `opened_at` ile iş gününe düşer.
  - Kaynak gerçeği `orders.orders`'tır (`source` Qr/Online; kural 1). Her sipariş tam bir kovaya düşer:
    - onay bekliyor (Draft, Submitted, PendingConfirmation);
    - kabul (Accepted, Preparing, Ready, Served, Completed);
    - ret (Rejected);
    - iptal (Cancelled).
    Kovaların toplamı alınan siparişe eşittir.
  - Değer = `orders.total` (kabul ve iptal için ayrı).
  - Sağlayıcı reddi = hiç yerel siparişe dönüşmemiş sağlayıcı siparişi; sağlayıcı sipariş numarası başına bir kez
    sayılır. Yinelenen webhook bir kez sayılır, sonradan yeniden işlenip sipariş olan sayılmaz.
  - Mutabakat farkı: V0-DOM-008 "Reconciliation backlog" tanımıdır (açık vaka sayısı, farklılık türüne göre).
    V12-REC-001'in `OnlineOrderMismatch` vakaları açık/kapalı adet ve açık tutarla verilir. Yeniden deneme sayısı
    V12-REC-001 retry izinden gelir.
  - Mutabakat toplamı (kural 2): siparişlerin adet ve kabul değeri defterden tek sorguyla ayrıca okunur ve
    satırların toplamıyla karşılaştırılır (`check.isBalanced`).
  - Rapor sürümü `channel-report.v1`, uç `/api/v1/management/reports/channels`, yetki `reports.view`.

## In scope

- Kaynak/kanal boyutları, iş tarihi filtreleri, order değeri, ret/iptal sayıları ve mutabakat farkı.

## Out of scope

- Metrik tanımı değişiklikleri, operasyonel komut yönetimi ve birleştirilmiş alanlar arası kontrol paneli.

## Dependencies

- V0-DOM-008
- V12-REC-001
- V12-ONL-005

## Deliverables

- Sürümlendirilmiş kanal raporu sorguları/API.
- İptalleri, yeniden denemeleri, saat dilimlerini ve yinelenen webhook'larnı kapsayan altın veri kümesi testleri.

## Acceptance evidence

- Rapor toplamları, aynı iş tarihi aralığı için onaylanmış order ve mutabakat kaynağı kayıtlarıyla mutabakat sağlar.
- `V12-REC-001` kanıtlı `NotApplicable` ise kanal raporları onaylanmış order kaynaklarıyla yine mutabakat sağlar;
  mutabakat vaka kaynağı beklenmez.
- Kapanış kanıtı (2026-09-26, gerçek PostgreSQL 18 UTF8, port 56433): modül testleri 4/4, Host HTTP testleri 12/12
  (2 tanesi yeni), MigrationComposition 161/161 yeşil. Sonuçlar:
  - Altın veri kümesi (iki iş günü; 21:30 UTC'nin ertesi iş gününe düştüğü sınırlar; bütün durum kovaları; iptaller;
    iki webhook'la gelen bir ret; sonradan yeniden işlenip sipariş olan bir ret; mutabakat vakaları ve retry'lar)
    beklenen raporu birebir verir. Defter toplamı (8 sipariş, 525,50 kabul değeri) satırlarla eşleşir. Aynı veriyle
    iki çalıştırma bayt bayt aynıdır.
  - Kaynak süzgeci yalnız o kanalı ve kendi defter toplamını verir; QR raporunda mutabakat vakası ve retry yoktur.
  - Yalnız sağlayıcı reddi olan gün de görünür.
  - Ters aralık, 31 günden uzun aralık ve kanal olmayan kaynak reddedilir; HTTP'de Türkçe `VALIDATION_FAILED`
    döner.
  - Yalnız görüntüleme yetkili yönetici raporu okur; oturumsuz çağrı 401, izinsiz çağrı 403 alır.
- Mutasyon kontrolü (dosya yedekten geri yüklenip `cmp` ile doğrulandı): 10 mutasyonun 10'u da bir testi kırmızıya
  çevirdi. Denenen mutasyonlar:
  - iş günü saat dilimi kaldırıldı (sorguda ve pencere hesabında);
  - kabul kovasından Completed çıkarıldı;
  - yeniden işlenmiş ret hariç tutulmadı;
  - ret webhook başına sayıldı;
  - defter toplamında kaynak süzgeci yok sayıldı;
  - Cashier kanala eklendi;
  - açık vaka sayımından Open çıkarıldı;
  - 31 gün sınırı gevşetildi;
  - QR raporuna retry sayısı eklendi.
- Kanıt: `evidence/V12-RPT-001/`.

## Handoff

- V15-RPT-001

# V1-RMD-791 - Gün sonu kapanışı (backend): sunucu hesaplı sayılar, ön koşul uyarıları, bahşiş ayrı satır, tek iş günü penceresi, tutarlı okuma

- Task ID: V1-RMD-791
- Status: InProgress
- Assignee: claude-code-session_019vXeYXjE3sM6VZKpRMj1L9
- Work type: implementation
- Surface state: Existing

## Goal

Kasa incelemesinin 4. satırı (`EndOfDayEndpoints.cs:83-84` iptal ve yazıcı hata sayıları istemciden; ön koşul yok; ciro bahşişi içerir) ve 1.020 kayıtlı doğrulama programından F0271 (garson/yazdırma özetleri ve iptal/yazıcı sayıları istemciden), F0729 (gün sonu toplamı işlem dışı okunuyor; özet kayıtlarının tarihi doğrulanmıyor), F0292 (iş günü kesimi belge-kod uyumsuzluğu), F0293 (bahşiş/hizmet bedeli kuralı), F0270 (masa kırılımı ve mutabakat toplamları yok), F0422 (okuyucu özeti iade dışlamasını anlatmıyor) kapanır. PO kararları (2026-11-03) aynen uygulanır:

1. Gün sonu kapanışı ön koşulları sunucuda okunur: açık kasa oturumu (`Open`/`Counting`/`Closing`), açık hesap (`Paid`/`Cancelled` dışı) ve sonucu belirsiz ödeme (`Pending`/`Unknown`/`ReconciliationRequired`; V1-RMD-790'daki tanım). Biri varsa uyarı + bilinçli onay (`acknowledgeOpenItems`) ile kapanır, engel yoktur; onaysız 409 `BUSINESS_DAY_OPEN_ITEMS_NOT_ACKNOWLEDGED`. Sayılar ve onay bayrağı kapanışla aynı işlemde `audit` olayı (`reporting.business_day_closed`) ve gün kaydı olarak yazılır. Kapanıştan önce aynı sayıları gösteren `GET .../{businessDate}/close-preflight` eklenir.
2. İptal ve yazıcı hata sayıları istemciden DEĞİL sunucudan hesaplanır: iptal = pencerede yazılmış `Order.ItemVoided` ve `Order.SentItemVoided` audit olayları (kalem iptalinin tek zaman damgalı kaydı); yazıcı hatası = pencerede `failed_at` yazılmış `kitchen.print_jobs` satırları (bir işi sonradan basılsa da hata sayılır; "kurtarılan" = hata aldıktan sonra `Printed` olan iş). İstekten `cancelledItems`/`printFailures` kalkar.
3. İstemciden gelen garson ve yazdırma özetleri kaldırılır (doğrulanamaz veri); yerine sunucu hesaplar: yazdırma özeti `kitchen.printers.station_id` kırılımında, garson özeti `orders.serving_user_id` kırılımında (sipariş sayısı, sipariş toplamı, iptal sayısı, indirim toplamı). Gün kaydında ayrıca masa kırılımı (`orders.table_id`: sipariş sayısı, sipariş toplamı) ve kasa mutabakat toplamları (pencerede kapanan oturum sayısı, beklenen, sayılan, fark, yönetici onaylı kapanış sayısı, kendi mutabakatı sayısı) kapanışta saklanır (F0270).
4. Bahşiş AYRI satırdır ve ciroya DAHİL DEĞİLDİR: `total_tips_amount` yeni sütun; ciro = onaylı ödemeler - iadeler - bahşiş. Bahşiş = pencerede kapanmış (`Paid`) hesapların `Tip` düzeltmeleri toplamı (kodun mevcut bahşiş havuzu tanımıyla aynı kaynak: `billing.bill_adjustments`). İptal edilmiş (iade edilerek kapanmış) hesabın bahşişi sayılmaz, böylece iadede ikinci kez düşülmez. Mevzuat notu (RESULT.md ve belgeye): bahşiş gönüllüdür, KDV matrahına eklenmez; kartla bahşişin yasal/muhasebe işlenişi mali müşavirce TEYİT EDİLMEMİŞTİR, kod yalnız ayrı alan sağlar.
5. Tek iş günü penceresi: `BusinessDayWindow` (06:00 Europe/Istanbul - ertesi gün 06:00) tek yerde tanımlanır; `BusinessDayTotalsReader.cs` ve ödeme mutabakat raporu (`Payments/ReportingModels.cs`) ile kanal raporu (`Channels/ChannelReportModels.cs`, gece yarısı penceresi kullanıyordu: aynı "iş günü" ifadesi, farklı pencere) bunu çağırır.
6. F0729: toplamlar, ön koşul sayıları ve kapanış güncellemesi AYNI işlemde okunur/yazılır (okuma tek SQL deyimi = tek anlık görüntü; kilit ve `FOR UPDATE` altında). Okuyucu işlem içi aşırı yüklemesi alır. Ön koşul onayı ve rakamlar kapanış işleminde yeniden hesaplanır (önizleme ile kapanış arasında değişen sayı onaysız geçmez).
7. F0292/F0293 belge düzeltmesi: `money-tax-business-date.md` onaylı kararı "23:59:59 kesim" der; kod ve tüm operasyon raporları 06:00 pencere kullanır. Belgeye, vergi/fatura `service_date` ile operasyonel iş günü penceresini ayıran ve mevcut davranışı açıkça yazan bir ek bölüm eklenir; bahşiş kuralı notu eklenir (onaylı karar satırlarına dokunulmaz).
8. F0422: `IBusinessDayTotalsReader` özet yorumuna iade dışlaması yazılır.

Kapsam dışı: arayüz (V1-RMD-792); kasa kapanışı (V1-RMD-790); fiskal/Z raporu (dış bağımlı); `ShiftSummaryStore` UTC günü (garson bütçesi, kasa değil).

## Owned surface

- `evidence/V1-RMD-791/**`
- `database/migrations/V1/V1-RMD-791/**`
- `src/Modules/Reporting/BusinessDayTotals/BusinessDayWindow.cs`
- Sınırlı ek — yollar ilgili görevlerin sahipliğinde kalır (geri-tik olmadan):
  - src/Modules/Reporting/BusinessDayTotals/BusinessDayTotalsReader.cs (V1-RMD-421) — tek deyimli sunucu hesapları, işlem içi okuma.
  - src/Modules/Reporting/Payments/ReportingModels.cs (V13-RPT-001) — ortak pencere.
  - src/Modules/Reporting/Channels/ChannelReportModels.cs (V12-RPT-001) — ortak pencere.
  - src/Modules/Reporting/Channels/ChannelReportService.cs (V12-RPT-001) — satırların iş günü etiketi de 06:00 kaymasıyla (pencere ile aynı gün tanımı).
  - src/Modules/Reporting/V1Operations/OperationalReportService.cs (V1-RPT-001) — sunucu hesaplı kapanış, ön koşul.
  - src/Modules/Reporting/V1Operations/PostgresOperationalReportRepository.cs (V1-RPT-001) — işlem içi kapanış, yeni sütunlar.
  - src/Modules/Reporting/V1Operations/ReportingModels.cs (V1-RPT-001) — bahşiş, masa, mutabakat kayıtları.
  - src/Modules/Reporting/V1Operations/ReportingExceptions.cs (V1-RPT-001) — onaysız ön koşul istisnası.
  - src/Host/Experience/Reporting/EndOfDayEndpoints.cs (V1-RMD-249) — istek/yanıt sözleşmesi, önizleme ucu, audit.
  - src/Host/Composition/Errors/ApiErrorCatalog.cs (V1-RMD-431) — yeni hata kodu.
  - tests/Host/Errors/ApiErrorCatalogTests.cs (V1-RMD-431) — yeni eşleme.
  - database/MigrationComposition/order.json (V1-FND-004) — yeni migration kaydı.
  - src/Host/Composition/Migrations/MigrationManifest.cs (V1-FND-004) — yalnız PhaseBMax.
  - tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V1-GOV-007) — yalnız yeni migration kimliği.
  - tests/Architecture/ApiConventions/api_convention_allowlist.json (V1-RMD-429) — yeni uç gerekirse.
  - tests/Host/Experience/Reporting/EndOfDayHttpTests.cs (V1-RMD-249) — sözleşme ve kurallar.
  - tests/Host/Experience/Reporting/EndOfDayTestDatabase.cs (V1-RMD-249) — test verisi yardımcıları.
  - tests/Host/Experience/Reporting/EndOfDayServerFiguresTests.cs (V1-RMD-249) — yeni dosya: sunucu hesapları ve bahşiş testleri.
  - tests/Host/Experience/Reporting/BusinessDayOrderCountRefundTests.cs (V1-RMD-511) — imza uyumu.
  - tests/Host/Experience/Reporting/BusinessDayRefundNettingTests.cs (V1-RMD-507) — bahşiş ve iade uyumu.
  - tests/Modules/Reporting/V1Operations/PostgresOperationalReportRepositoryTests.cs (V1-RPT-001) — kapanış deposu.
  - tests/Modules/Reporting/V1Operations/ReportingDomainTests.cs (V1-RPT-001) — servis testleri.
  - tests/Modules/Reporting/Channels/ChannelReportTests.cs (V12-RPT-001) — ortak pencere uyumu.
  - tests/Modules/Reporting/Payments/PaymentSettlementReportTests.cs (V13-RPT-001) — ortak pencere uyumu.
  - tests/Modules/Reporting/V1Operations/BusinessDayWindowTests.cs (V1-RMD-421) — yeni dosya: pencere testleri (V1Operations test projesi BusinessDayTotals kaynağını derler).
  - tests/Modules/Reporting/V1Operations/ALKAROS.Reporting.V1Operations.Tests.csproj (V1-RPT-001) — elle seçilmiş göç listesine 214.
  - docs/compliance/money-tax-business-date.md (V0-CMP-002) — yalnız ek bölüm (onaylı karar satırları değişmez).
  - tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs (V1-RMD-659) — yalnız Host.Experience.Reporting için Audit kenarı (kapanış denetim olayı).
  - docs/architecture/module-dependency-rules.md (V1-RMD-659) — yalnız aynı kenarın tablo satırı.
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

1. Her kural için önce kırmızı test (gerçek Postgres), sonra düzeltme, sonra yeşil.
2. Bahşiş: ödenmiş hesap, iade edilip iptal olmuş hesap, açık hesap, kısmi iade durumları; ciro bahşişsiz, bahşiş ayrı.
3. Önizleme ile kapanışın aynı sayıları verdiği, onaysız kapanışın 409 aldığı ve onaylı kapanışın sayılarıyla audit'in yazıldığı kanıtlanır.
4. Pencere sınırları: 05:59:59 önceki gün, 06:00:00 o gün; kış/yaz saati; üç rapor aynı pencereyi kullanır.
5. İptal sayısı yalnız iki kalem iptal olayından, yazıcı sayısı yalnız pencere içi `failed_at` satırlarından gelir.

## Out of scope

- Arayüz (V1-RMD-792); kasa kapanışı (V1-RMD-790); mali müşavir teyidi gerektiren kartla bahşiş muhasebesi (kod ayrı alan sağlar).

## Dependencies

- V1-RMD-790

## Acceptance evidence

1. Kırmızı sonra yeşil: Host Reporting ve Modules/Reporting testleri küçük gruplarla koşulur.
2. Migration ileri ve geri boş veritabanında çalışır (`evidence/V1-RMD-791/`).
3. Mutasyon: her kural bozulunca en az bir test kırılır; kaynak `cmp` ile geri alınır.
4. ApiConventions, ModuleBoundaries yeşil; plan audit ve consistency audit 0 hata/0 uyarı; kapsam aracı exit 0.
5. qa-backend ve qa-test-kalitesi incelemeleri sırayla yapılır, bulgular giderilir, düzeltmelerden sonra kısa doğrulama turu yapılır.

## Handoff

- V1-RMD-792

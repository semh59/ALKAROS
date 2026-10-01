# V1-RMD-487 - Ürün satış maliyeti ve brüt kâr raporu

- Task ID: V1-RMD-487
- Status: InProgress
- Assignee: claude-code-session_01XpoF59o3sDPfb7ZADR4BMf
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-10-01

## Goal

Projede satılan ürün başına maliyet ve kâr gösteren rapor yok. Bu görev, tarih aralığında ürün başına satılan adet, KDV hariç net satış, satışın stoktan gerçekten tükettiği malzemenin
(ürün bağlantısı ve ekstralar, iptal edilen düşümler çıkarılmış) satış günündeki ağırlıklı ortalama alış maliyeti ve brüt kâr üreten salt okunur, sürümlü bir rapor ve yönetici uç noktası ekler.
Alış maliyeti bilinmeyen kalem sıfır sayılmaz; "maliyet bilinmiyor" olarak işaretlenir ve sayılır.

## Owned surface

- `plan/v1/remediation/V1-RMD-487-product-margin-report.md`
- `evidence/V1-RMD-487/**`
- `src/Modules/Reporting/ProductMargin/**`
- `tests/Modules/Reporting/ProductMargin/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/ReportingModule.cs — yalnız servis kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/Reporting/ProductMarginReportEndpoints.cs — yeni uç nokta dosyası
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs — yalnız uç noktanın eşlenmesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx — yalnız yeni test projesi
- Sınırlı ek (paylaşılan, geri-tik olmadan): docs/domain/reporting-metrics.md — yalnız yeni metrik satırı
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Kaynak: ödenmiş (`Paid`) fişlerin `Sale` ve `Complimentary` kalemleri; fiş günü `Europe/Istanbul` takvim günüdür (onaylı iş günü kararı: sipariş açıldığı gün). Çevrimiçi siparişin fişi olmadığından hariçtir; iptal edilmiş fişler hariçtir.
- Net satış: kalemin `net_amount` değeri (ekstralar dahil, KDV hariç). İkramlar satışa katkı vermez ama adet ve maliyetle ayrıca gösterilir. Fiş düzeyi indirim ve hizmet bedeli ürüne dağıtılmaz; toplamı ayrı satırda gösterilir.
- Maliyet: kalemin `Consumption` stok hareketleri eksi geri alınanlar; her stok kalemi için satış gününe kadarki alış girişlerinin ağırlıklı ortalaması. Ekstra de aynı hareketlerden geldiği için iki kez sayılmaz.
- Kontrol bloğu: ürün satırları toplamı ile bağımsız sorgulanan kalem toplamı. En çok 31 gün, en çok 1000 ürün satırı.
- `GET /api/v1/management/reports/product-margin?from=&to=`; `reports.view` yetkisi, Türkçe doğrulama hataları.

## Out of scope

- Yönetim ekranı bölümü (`V1-RMD-488`); kategori kırılımı; çevrimiçi sipariş marjı; fiş düzeyi indirimin ürüne dağıtılması.

## Dependencies

- V1-RMD-486

## Acceptance evidence

- Testler, mutasyon kanıtı ve gerçek Host denemesi; çıktılar `evidence/V1-RMD-487/` altındadır.

## Handoff

- V1-RMD-488

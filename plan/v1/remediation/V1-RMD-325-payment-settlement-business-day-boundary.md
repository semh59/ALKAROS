# V1-RMD-325 - EOD raporu ile ödeme mutabakat raporu gün sınırı tutarsızdı

- Task ID: V1-RMD-325
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-26

## Goal

Bağımsız 13 ajanlı derin denetimin (2026-09-26) K15 bulgusu: `OperationalReportService.CalculateServiceWindow` (EOD, şu an dead code — hiçbir HTTP endpoint'ten çağrılmıyor) iş gününü 06:00 → ertesi gün 05:59:59.999 olarak tanımlıyor; `PaymentSettlementReportFilter.ResolveWindow()` (gerçek, canlı bir HTTP endpoint'e bağlı) düz yerel gece yarısını (00:00 → ertesi 00:00) kullanıyordu. Gece 01:00'de ödenen bir hesap iki farklı raporda iki farklı günün cirosuna düşebiliyordu — bir restoran gece yarısında kapanmıyor, 01:00'de hâlâ hizmet veren masa hangi işletme gününe aitse (önceki akşamın), her iki rapor da AYNI günü göstermeli.

## Owned surface

- `plan/v1/remediation/V1-RMD-325-payment-settlement-business-day-boundary.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Modules/Reporting/Payments/ReportingModels.cs
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Modules/Reporting/Payments/PaymentSettlementReportTests.cs

## In scope

1. `PaymentSettlementReportFilter.ResolveWindow()`'un iş günü sınırı `CalculateServiceWindow`'un zaten tanımladığı AYNI 06:00 sınırına hizalanır (dahil-hariç `[start, end)` biçimi korunarak — `CalculateServiceWindow`'un kendi 05:59:59.999 yaklaşımındaki 1ms boşluk riski yerine daha temiz `end = ertesi gün 06:00` kullanıldı).

## Out of scope

- `ChannelReportFilter.ResolveWindow()` (V12-RPT-001, online sipariş kanal raporu) — AYNI hata sınıfını (düz yerel gece yarısı, kendi belge yorumuyla çelişerek) taşıdığı incelemede bulundu, ama bu görev sırasında değiştirilirse `ChannelReportTests.cs`'in özenle hesaplanmış, kuruşa kadar dengelenmiş altın veri seti testinin (`TheGoldenDatasetProducesExactlyTheExpectedChannelReportAndBalancesToTheLedger`) BEKLENEN TÜM rakamlarının yeniden hesaplanmasını gerektirir (birden fazla sipariş gece yarısı sınırında bilerek konumlandırılmış) — riskli, büyük, bu bulgunun kapsamı dışında ayrı bir görev olmalı. Not düşülüyor, düzeltilmedi.
- `CalculateServiceWindow`'un kendisi (EOD, hâlâ dead code) — kendi 1ms boşluğu (05:59:59.999) ayrı, küçük bir orta seviye bulgu; bu görev yalnız iki raporun birbiriyle TUTARLI olmasını sağladı.
- `qr_ordering`/EOD'nin gerçek iş günü sınırının GERÇEKTEN 06:00 mı olması gerektiği (bir ürün kararı, Semih'in belge yorumunda zaten ima ediliyor) — bu görev yalnız iki MEVCUT tanımı birbirine hizaladı, üçüncü bir değeri icat etmedi.

## Dependencies

- None

## Acceptance evidence

Host/Modules testleri (UTF8 Postgres 18), gerçek bir veritabanına karşı: yeni test `APaymentAtOneAmLocalTimeBelongsToThePreviousBusinessDateNotTheNewCalendarDay` — Europe/Istanbul yerel saatiyle 15 Haziran 01:00'de (UTC 14 Haziran 22:00) onaylanmış bir ödeme seed edilir; 15 Haziran'ın raporunda GÖRÜNMEDİĞİ (artık gece yarısı değil 06:00 sınırı), 14 Haziran'ın raporunda GÖRÜNDÜĞÜ (önceki iş gününe ait) doğrulanıyor. `ALKAROS.Reporting.Payments.Tests` 10/10 (1 yeni), regresyon yok (mevcut testlerin hepsi sınırdan uzak saatlerde seed edildiği için etkilenmedi).

Mutasyon kontrolü: `ResolveWindow()`'un sınırı geçici olarak eski hâline (`TimeOnly.MinValue`, gece yarısı) döndürüldü — yeni test gerçekten kırmızı oldu (ödeme yanlışlıkla 15 Haziran'ın raporunda göründü); dosya `diff` ile birebir orijinaline geri getirildi, tüm paket tekrar yeşil.

## Handoff

- None

# V1-RMD-768 - Süresi dolmuş "Printing" işi körlemesine yeniden basılmaz

- Task ID: V1-RMD-768
- Status: InProgress
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-739 bulguları V1-KIT-003 ve V1-KIT-004: `PrintQueueService.ProcessEligibleJobsAsync` işi gönderimden önce `Printing` olarak kalıcılaştırır. Çalışan süreç gönderim sırasında veya sonrasında çökerse iş `Printing` ve süresi dolmuş kalır; `RecoverExpiredLeasesAsync` (her 5 sn) bunu körlemesine `Pending` yapar ve bilet yeniden basılır, oysa aynı sınıftaki belirsiz çıktı yolu (`PrinterTransmissionUncertainException`) çift basımı önlemek için işi `AwaitingOperatorReview`'a koyar.

Kurtarma iki yola ayrılır: süresi dolmuş `Leased` (hiç gönderilmemiş) `Pending`'e döner; süresi dolmuş `Printing` (çıktısı bilinmiyor) `AwaitingOperatorReview` olur ve nedeni yazılır. Alan nesnesindeki `ResetExpiredLease` aynı kuralı uygular.

V1-KIT-004'ün "gönderimden önce kalıcı işaret yok" kısmı iş düzeyinde `Printing` kaydıyla zaten vardır; bu görev o işareti kurtarma kararına bağlar. `physical_print_deliveries` içinde InFlight satırı açmak kapsam dışıdır.

## Owned surface

- `plan/v1/remediation/V1-RMD-768-yazdirma-isi-kurtarma-belirsiz-cikti.md`
- `evidence/V1-RMD-768/**`
- Sınırlı ek: src/Modules/Kitchen/PrintQueue/PostgresPrintQueueRepository.cs
- Sınırlı ek: src/Modules/Kitchen/PrintQueue/PrintJob.cs
- Sınırlı ek: tests/Modules/Kitchen/PrintQueue/PrintQueueUnitTests.cs
- Sınırlı ek: tests/Modules/Kitchen/PrintQueue/PostgresPrintQueueIntegrationTests.cs

## In scope

- `RecoverExpiredLeasesAsync`: `Leased` → `Pending`, `Printing` → `AwaitingOperatorReview` (hata metni nedenini yazar).
- `PrintJob.ResetExpiredLease`: aynı ayrım.
- Birim testi ve gerçek PostgreSQL testi; mevcut `Leased` → `Pending` testleri geçmeye devam eder.

## Out of scope

- `physical_print_deliveries` InFlight kaydı, operatör onay ekranı eylemleri ve yeniden yazdırma kararı.
- Köprü, yazıcı taşıma ve yeniden deneme/backoff.

## Dependencies

- V1-KIT-003
- V1-KIT-004

## Acceptance evidence

1. Düzeltmeden önce yeni testler kırmızı olur; çıktı kaydedilir.
2. Düzeltmeden sonra `PrintQueue` test projesi (gerçek PostgreSQL) exit 0 geçer.
3. Mutasyon: `Printing` dalı `Pending`'e geri alınırsa (SQL ve alan) ilgili testler kırmızı olur; dosya hash'leri geri yüklenir.
4. Kapsam aracı InProgress baseline'a karşı exit 0 verir; iki bağımsız incelemede artık bulgu kalmaz.

## Handoff

- None

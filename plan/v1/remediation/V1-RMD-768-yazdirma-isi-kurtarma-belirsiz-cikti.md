# V1-RMD-768 - Süresi dolmuş "Printing" işi körlemesine yeniden basılmaz

- Task ID: V1-RMD-768
- Status: Planned
- Assignee: claude-code-session_01FMN5WVXi1HJCTEk5argkgZ
- Work type: remediation
- Surface state: Existing

## Goal

V1-RMD-739 bulguları V1-KIT-003 ve V1-KIT-004: `PrintQueueService.ProcessEligibleJobsAsync` işi gönderimden önce `Printing` olarak kalıcılaştırır. Çalışan süreç gönderim sırasında veya sonrasında çökerse iş `Printing` ve süresi dolmuş kalır; `RecoverExpiredLeasesAsync` (her 5 sn) bunu körlemesine `Pending` yapar ve bilet yeniden basılır, oysa aynı sınıftaki belirsiz çıktı yolu (`PrinterTransmissionUncertainException`) çift basımı önlemek için işi `AwaitingOperatorReview`'a koyar.

Kurtarma iki yola ayrılır: süresi dolmuş `Leased` (hiç gönderilmemiş) `Pending`'e döner; süresi dolmuş `Printing` (çıktısı bilinmiyor) `AwaitingOperatorReview` olur ve nedeni yazılır. Alan nesnesindeki `ResetExpiredLease` aynı kuralı uygular.

Operatör incelemesi `physical_print_deliveries` içindeki `Unknown` kayıtlarından listelenir ve yeniden basım onayı o kayda bağlıdır (`KitchenOperationsEndpoints`). Kurtarma yalnız işin durumunu değiştirirse iş kimseye görünmez ve yeniden basılamaz; "bilet hiç basılmadı" riski artar. Bu yüzden kurtarma, işi inceleme beklemeye alırken aynı işlemde ona bağlı bir `Unknown` teslimat kaydı da yazar (belirsiz-çıktı yolunun ürettiği biçimle aynı).

## Owned surface

- `plan/v1/remediation/V1-RMD-768-yazdirma-isi-kurtarma-belirsiz-cikti.md`
- `evidence/V1-RMD-768/**`
- Sınırlı ek: src/Modules/Kitchen/PrintQueue/PostgresPrintQueueRepository.cs
- Sınırlı ek: src/Modules/Kitchen/PrintQueue/PrintJob.cs
- Sınırlı ek: tests/Modules/Kitchen/PrintQueue/PrintQueueUnitTests.cs
- Sınırlı ek: tests/Modules/Kitchen/PrintQueue/PostgresPrintQueueIntegrationTests.cs

## In scope

- `RecoverExpiredLeasesAsync`: `Leased` → `Pending`, `Printing` → `AwaitingOperatorReview` (hata metni nedenini yazar) ve aynı komutta işe bağlı `Unknown` teslimat kaydı (açık teslimatı olan iş için yenisi yazılmaz).
- `PrintJob.ResetExpiredLease`: aynı ayrım.
- Birim testi ve gerçek PostgreSQL testi (kurtarılan işin `Unknown` teslimatı operatör listesinde görünür, karışık `Leased`+`Printing` durumu, ikinci kurtarma turu çift kayıt yazmaz); mevcut `Leased` → `Pending` testleri geçmeye devam eder.

## Out of scope

- Operatör onay ekranı eylemleri ve yeniden yazdırma kararının kendisi (var olan akış kullanılır).
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

# V1-RMD-564 - Sapma motorunun Host işi

- Task ID: V1-RMD-564
- Status: InProgress
- Assignee: claude-code-session_01UKcG8ALDsRweQYx6F2bSYe
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-10-15

## Goal

V1-RMD-536 sapma motoru 5 dakikalık Host işiyle çalışır: normalden sıklaşan açık hatanın şiddeti yükselir ve susan dedektör olay üretir.

## Owned surface

- `plan/v1/remediation/V1-RMD-564-analysis-runner.md`
- `evidence/V1-RMD-564/**`
- `src/Host/Observability/AnalysisRunner/**`
- `tests/Host/Observability/AnalysisRunner/**`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/DualScreen/DualScreenApplication.cs - yalnız iş kaydı
- Sınırlı ek (paylaşılan, geri-tik olmadan): ALKAROS.slnx - yalnız yeni test projesi girişi
- Bu görev, başka bir görevin owned surface alanını yukarıdaki sınırlı ekler dışında değiştiremez.

## In scope

- Son 10 dakikada görülen açık hatalar için taban ve mevcut pencere okunur; eşik aşılırsa yazıcıya bir kademe yüksek şiddette aynı hata verilir.
- İşin kendi nabzı kaydedilir ve susan dedektörler `monitoring.detector_degraded` olayı olur.

## Out of scope

- Diğer arka plan işlerinin nabız atması (kendi görevlerinde), açılış öz testi.

## Dependencies

- V1-RMD-536
- V1-RMD-534

## Acceptance evidence

1. Geçmişi olan bir hatanın şiddeti taban aşılınca bir kademe yükselir; yeterli geçmişi olmayan yükselmez.
2. Susan kayıtlı dedektör olay üretir; bir tur hata verirse döngü sürer.
3. Çıktılar `evidence/V1-RMD-564/` altındadır.

## Handoff

- None

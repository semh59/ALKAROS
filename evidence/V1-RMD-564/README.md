# V1-RMD-564 kanıtı

V1-RMD-536 sapma motoru 5 dakikalık Host işiyle çalışır (`AnalysisRunnerHostedService`, `DualScreenApplication.cs` içinde kayıtlı). Son 10 dakikada görülen açık, Kritik olmayan hatalar için aynı hafta günü/saat tabanı ve mevcut pencere okunur; Z-skor eşiği aşılırsa aynı hata bir kademe yüksek şiddetle yazıcıya verilir (yazıcının mevcut yükseltme mantığı satırı günceller, uyarı/bildirim köprüleri tetiklenir). İşin kendi nabzı kaydedilir; susan kayıtlı dedektör `monitoring.detector_degraded` olayı olur.

## Kabul koşulları
1. Geçmişi olan hata (5 hafta) tabanı aşınca Warning→Error; yeterli geçmişi olmayan yükselmez (AbnormallyFrequentErrorIsReReportedOneLevelHigher, ErrorWithoutEnoughHistoryIsNotEscalated).
2. Susan dedektör olay üretir (SilentRegisteredDetectorBecomesADegradedEvent); hatalı tur sayılır ve döngü sürer (FailingRoundIsCountedAndTheLoopKeepsRunning).
3. Test 4/4 geçti; mutasyon 5/5 öldürüldü.

## Sınır
- Diğer arka plan işleri (sağlık, kaynak, içgörü, sonda) henüz nabız atmıyor; yalnız bu işin kendi nabzı kayıtlı. Her biri nabız atınca susma tespiti genişler (kendi dosyalarında küçük bir değişiklik).
- Canlı uygulamada 5 dk periyodu ve gerçek yükselme gözlenmedi; açılışta öz test yok.

# V1-RMD-569 - Sunucu dedektör koşucularının nabzı

Ne yapıldı
- `ServerDetectorsDeepRunner` başlarken `server-detectors-deep` adıyla nabza kaydolur ve her turdan sonra nabız verir; ana sunucuda mevcut `DetectorHeartbeats` kaydı kullanılır. Koşucu iki tur süresinden uzun sessiz kalırsa mevcut "dedektör sessiz" raporu onu da kapsar (`monitoring.detector_degraded`).

Kabul koşulları
1. Nabız ve sessizlik: `RunnerBeatsItsHeartbeatEachRoundAndIsReportedSilentOnceItStops` (nabız yokken sessiz, tur sonrası temiz); nabızsız çalışma: `RunnerWithoutHeartbeatsStillRuns`.
2. Bu klasör; mutasyon 3/3 yakalandı.

Bilinen sınırlar (doğrulanamadı / yapılmadı)
- Kayıt temizleme işi (günlük) bu görevde nabza bağlanmadı; yalnız derin dedektör koşucusu.
- Ana sunucu kaydındaki (DI) nabız bağlantısı testle doğrulanmadı; koşucu testleri nabzı doğrudan verir.

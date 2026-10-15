# V1-RMD-534 kanıtı

Sunucu dedektörleri: 5xx istisnaları, veritabanı hata kodları ve yavaş istek olay olarak kaydedilir; olay yazıcısı Host'ta izleme havuzuyla bağlanır; saklama silme işi günlük zamanlanır ve denetim kaydı yazar.

## Kabul koşulları
1. 500 üreten istek = tam 1 Kritik `api.unhandled_exception`; 404 ve 409 = 0 olay (testler: UnhandledExceptionIsRecordedOnceAsCriticalAndNotSwallowed, ClientErrorsProduceNoEvent).
2. 55P03 hata kodu veritabanı olayı üretir (LockNotAvailableDatabaseFaultIsRecorded).
3. Eşik üstü yavaş istek 1 Warning, eşik altı 0 olay (SlowRequestOverThresholdIsOneWarningAndUnderIsNone).
4. Yazıcı, ara katman ve silme işi `DualScreenApplication.cs` içinde bağlandı; silme işi 30/90 gün politikasıyla çalışır ve `monitoring.retention.run` denetim kaydı yazar (RetentionRunPurgesOldRowsAndWritesOneAuditEvent).
5. Testler gerçek Postgres'e karşı: 5/5 geçti (`test-runs.log`). Mutasyon: 7 mutant, 7'si öldürüldü (`mutations.log`).

## Sınırlar
- Testler gözlemciyi doğrudan çağırır; tam uygulama ayağa kalkıp gerçek bir isteğin zincirde gözlemciye ulaşması bu görevde ölçülmedi (doğrulanamadı; V1-RMD-556 yük ölçümünde bakılır).
- Havuz doluluğu, kaçan arka plan işi, N+1, yavaş sorgu ve EXPLAIN V1-RMD-558'e taşındı.

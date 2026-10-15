# V1-RMD-563 kanıtı

V1-RMD-546 veritabanı içgörüsü artık 60 sn aralıklı Host işiyle (`InsightRunnerHostedService`, `DualScreenApplication.cs` içinde kayıtlı) izleme havuzundan okunur; bulgular hata olayı yazıcısına verilir.

## Kabul koşulları
1. Gerçek kilit zinciri bir turda `db.lock_wait` olarak veritabanına yazıldı (RealLockChainBecomesAStoredLockWaitEvent).
2. Bir tur hata verirse (erişilemeyen veritabanı) döngü sürer ve hata sayılır (FailingRoundIsCountedAndTheLoopKeepsRunning).
3. Test 2/2 geçti; mutasyon 3/3 öldürüldü; Host DualScreenHostTests 6/6 geçti.

## Doğrulanamadı / sınır
- İş canlı uygulamada başlatılıp 60 sn periyodu gözlenmedi.
- Aynı kilit uzun sürerse her turda yeni olay (aynı parmak izi) birleşir; bildirim tekrarını 538'in bastırma penceresi sınırlar.

# V1-RMD-555 kanıtı

`CaptureSystemHealthSnapshotAsync` artık 60 sn aralıklı arka plan işiyle çağrılıyor (`HealthSnapshotJob`, `DualScreenApplication.cs` içinde kayıtlı). Veritabanı erişimi ve boyutu, disk boş/toplam bayt değerleri gerçek ölçülür; yedek durumu mevcut servis tarafından son yedekten hesaplanır.

## Kabul koşulları
1. Görüntü 60 sn aralıkla yazılır: iş varsayılan 60 sn (kod sabiti). Disk boş/toplam ve veritabanı boyutu sıfırdan büyük gerçek değer (CaptureWritesRealDatabaseSizeAndDiskValues, DefaultDiskReportsRealNonZeroValues); disk eşikleri %10 Degraded, %5 Unhealthy; erişilemeyen veritabanı Unhealthy (UnreachableDatabaseIsReportedUnhealthyWithZeroSize).
2. Test 7/7 geçti (`test-runs.log`); mutasyon 6/6 öldürüldü (`mutations.log`).

## Doğrulanamadı
- Çalışan uygulamada yönetim ekranının değeri gösterdiği ve 60 sn'de bir yenilendiği gözle/çalıştırılarak doğrulanmadı; ekran mevcut `GetLatestHealthSnapshotAsync` yolunu okuyor (KitchenOperationsStore).
- "Yedek yaşı" ekranda ayrı bir değer olarak üretilmiyor; yalnız son yedeğin durumu (Healthy/Degraded/Unhealthy) yazılıyor.

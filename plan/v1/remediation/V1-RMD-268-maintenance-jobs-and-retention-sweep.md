# V1-RMD-268 - Zamanlanmış/elle tetiklenebilir bakım işleri altyapısı ve saklama süresi süpürmesi

- Task ID: V1-RMD-268
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

Erişilebilirlik envanteri: `V15-SEC-003` `RetentionExecutionService` ve `DeletionQueueProcessor`
(saklama süresi dolan hassas kayıtları imha etme, silme kuyruğunu boşaltma) yazılmış ve
DI'da kayıtlıydı; çalışan uygulamada çağıranı yoktu. Yedek yükleme ve geri yükleme doğrulaması
(V1-RMD-269/270) da aynı biçimde zamanlayıcı ister; bu yüzden ortak bir altyapı kuruldu:

1. `IMaintenanceJob` + `MaintenanceJobRunner` + `MaintenanceJobHostedService`: her iş kendi
   aralığında çalışır (ilk çalışma açılıştan 10 dk sonra, hepsi birden başlamaz), bir iş kendi
   üzerine binmez, hata ana süreci düşürmez, çalışamayan iş (eksik ayar) sessizce "başarılı"
   demez: `Skipped` + Türkçe neden bildirir. Hata mesajı yöneticiye sızdırılmaz (yalnız tür adı).
2. `GET /api/v1/management/security/maintenance/jobs` (durumlar) ve
   `POST .../maintenance/jobs/{name}/run` (şimdi çalıştır); yönetici oturumu + `security.manage`.
3. `retention-sweep` işi (24 saat): süpürme + silme kuyruğu; her ikisi de idempotent, bu yüzden
   elle çalıştırma günlük zamanlayıcıyla çakışmaz.

**Önemli bulgu (dürüst sınır):** uygulamanın HİÇBİR yeri `security.retention_subjects`
tablosuna kayıt eklemiyor (`IRetentionSubjectStore.InsertAsync` modül dışında çağrılmıyor).
Yani üretimde süpürme bugün hep 0 kayıt işler. Bu görev tetikleyiciyi bağlar; işlenecek
veriyi üretmek (ör. müşteri kişisel verisi, sağlayıcı yükleri) ilgili veri görevlerinin
(V14 müşteri verisi vb.) işidir ve orada `InsertAsync` çağrısı zorunlu kabul edilmelidir.

## Owned surface

- `plan/v1/remediation/V1-RMD-268-maintenance-jobs-and-retention-sweep.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/Maintenance/MaintenanceJobs.cs
  (V1-RMD-266 sahipliğindeki klasöre eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/Maintenance/RetentionSweepMaintenanceJob.cs
  (aynı sahiplikte yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs
  (yalnız bakım işi kayıtları ve iki uç nokta)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/MaintenanceJobRunnerTests.cs
  (aynı sahiplikte yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs
  (2 yeni test)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationTestDatabase.cs
  (yalnız saklama kaydı tohumlama yardımcıları)

## In scope

1. Bakım işi altyapısı ve `retention-sweep` işi.
2. İki yönetim uç noktası.
3. Birim testleri (devre dışı, hata, çakışma, bilinmeyen ad) ve gerçek Postgres HTTP testleri.

## Out of scope

- Yedek yükleme ve geri yükleme doğrulaması işleri (V1-RMD-269/270).
- Saklama kayıtlarını üretecek veri kaynakları.
- İş durumunun kalıcılaştırılması (durum bellekte; işler kendi kalıcı kanıtını yazar).
- Yönetim arayüzü.

## Dependencies

- V15-SEC-003
- V1-RMD-266

## Acceptance evidence

- Host.Experience.SecurityAdministration (UTF8 Postgres 18): 12/12. Yeni: gerçek tohumlanmış 3 kayıtla
  (süresi dolmuş, yasal tutuklu, yeni) süpürme yalnız süresi dolmuşu imha eder ("1 kayıt imha edildi"),
  denetim olayı bir kez yazılır, ikinci çalıştırma "0 kayıt imha edildi" ve olay sayısı artmaz;
  iş listesi/çalıştırma anonim 401, `reports.view`'lu yönetici 403, bilinmeyen ad 404; runner:
  devre dışı iş `Skipped` + neden, atan iş `Failed` (mesaj sızmaz), çakışan ikinci çalıştırma `Skipped`.
- `python tools/plan-audit/plan_audit_tool.py validate`, `consistency_audit.py` çalıştırıldı.

## Handoff

- None

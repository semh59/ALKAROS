# V1-RMD-270 - Uzak yedek yükleme işi (saatlik) ve dürüst RPO raporu

- Task ID: V1-RMD-270
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: remediation
- Surface state: Existing

## Source basis

- PO:2026-09-24

## Goal

Erişilebilirlik envanteri: `V15-BKP-001` `OffsiteBackupUploadService` (istemci tarafı şifreli,
sağlama toplamı doğrulamalı, yeniden denemeli uzak yedek) yazılmış ve DI'da kayıtlıydı;
çalışan uygulamada çağıranı yoktu. `backup.sh` yerel yedek üretiyor ama hiçbir şey onu uzağa
taşımıyordu.

Bakım işleri altyapısına (V1-RMD-268) `offsite-backup` işi eklendi (saatlik, elle de
çalıştırılabilir): `ALKAROS_BACKUP_DIR` altındaki, `.sha256` yan dosyası olan her `*.dump`
için — henüz hedefte yoksa — yan dosyadaki toplamla doğrulanır, `offsite-backup` gizli
anahtarının etkin sürümüyle şifrelenir, hedefe (`ALKAROS_OFFSITE_BACKUP_DIR`) yüklenir ve
makbuzu kaydedilir. Bir dosyanın hatası diğerlerini durdurmaz; hatalı dosyanın adı ve hata TÜRÜ
raporlanır (mesaj değil: yol/sır sızmasın). Çalışamıyorsa (kaynak dizin yok ya da yedek anahtarı
başlatılmamış) iş `Skipped` + Türkçe neden bildirir; sessizce "başarılı" demez.

`GET /api/v1/management/security/backup/rpo`: her veri sınıfı için hedef, ölçülen boşluk ve
hedefin karşılanıp karşılanmadığı.

**Dürüst sınır (RPO):** `pg_dump` bir an-görüntüsüdür; bu yüzden `Settings` (24 sa) sınıfı
olarak dosyalanır. `Fiscal` (5 dk) ve `OrdersInventory` (1 sa) hedefleri sürekli WAL
gönderimi ister; bu iş bunu YAPMAZ ve RPO raporu bu açığı `MeetsTarget=false` ile gösterir,
gizlemez. WAL gönderimi ayrı bir iştir.

Sınırlar: dosya bellekte okunur (servisin mevcut davranışı); 1 GiB üstü dosya atlanır ve
başarısız sayılıp raporlanır. Çalıştırma başına en fazla 10 dosya.

## Owned surface

- `plan/v1/remediation/V1-RMD-270-offsite-backup-job-and-rpo-report.md`
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/Maintenance/OffsiteBackupMaintenanceJob.cs
  (V1-RMD-266 sahipliğindeki klasöre eklenen yeni dosya)
- Sınırlı ek (paylaşılan, geri-tik olmadan): src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs
  (yalnız iş kaydı, `backup/rpo` uç noktası ve DTO)
- Sınırlı ek (paylaşılan, geri-tik olmadan): tests/Host/Experience/SecurityAdministration/SecurityAdministrationHttpTests.cs
  (2 yeni test ve geçici dizin kurulumu)

## In scope

1. `offsite-backup` bakım işi ve RPO uç noktası.
2. Gerçek şifreleme, gerçek dosya hedefi, gerçek Postgres makbuz deposu ile HTTP testleri.

## Out of scope

- WAL/temel yedek (`basebackup.sh`) yükleme ve sürekli WAL gönderimi.
- Gerçek bir uzak sağlayıcı (S3 vb.): hedef `ALKAROS_OFFSITE_BACKUP_DIR` ile bağlanan dizindir.
- Yedek üretimini zamanlamak (`backup.sh`'nin çağrılması dağıtım işidir).
- RPO ihlalinde otomatik uyarı.

## Dependencies

- V15-BKP-001
- V1-RMD-268
- V1-RMD-269

## Acceptance evidence

- Host.Experience.SecurityAdministration (UTF8 Postgres 18): 17/17. Yeni: kaynak dizin/anahtar yokken iş
  `Skipped` ve nedenini söyler; gerçek akış: 3 dosya (iyi, bozuk toplamlı, yan dosyasız) → yalnız iyi olan
  yüklenir, iş `Failed` ve bozuk dosyanın adını söyler, hedefte tek `.enc` dosyası var ve içinde düz metin
  işareti YOK; RPO raporu `Settings` karşılandı, `Fiscal`/`OrdersInventory` karşılanmadı; toplam düzeltilince
  ikinci çalıştırma "1 yedek yüklendi, 1 yedek zaten uzakta", üçüncüsü "0 ... 2 zaten uzakta", hedefte 2 dosya.
- `python tools/plan-audit/plan_audit_tool.py validate`, `consistency_audit.py` çalıştırıldı.

## Handoff

- None

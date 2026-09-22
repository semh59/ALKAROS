# V15-BKP-001 - Implement encrypted off-site backup

- Task ID: V15-BKP-001
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.38-I.44
- PDF:II.2.23
- PDF:III.25

## Goal

Doğrulanmış şifrelenmiş veritabanı yapılarını, saklama ve anahtar meta verileriyle birlikte doğrulanmış hedefe yükleyin.

## Owned surface

- `src/Modules/Operations/OffsiteBackup/**`, `tests/Modules/Operations/OffsiteBackup/**`,
  `database/migrations/V15/V15-BKP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  src/Modules/Operations/ALKAROS.Operations.csproj (V1-OPS-002 sahipliğinde) —
  yeni ProjectReference'lar eklendi: ALKAROS.Security.csproj (V15-SEC-001'in
  secret rotation'ı için), ALKAROS.Secrets.csproj, ALKAROS.SensitiveData.csproj
  (envelope cipher için), ALKAROS.Messaging.csproj (RetryPolicy'nin
  exponential backoff'u için), ALKAROS.Observability.csproj (yükleme
  hatasında structured alert için).
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  ALKAROS.slnx (V1-FND-001 sahipliğinde), build/project-manifest.json
  (V1-FND-007 sahipliğinde) — yeni
  ALKAROS.Operations.OffsiteBackup.Tests proje kaydı eklendi (C86/C88/C91
  emsali).
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs
  (V1-FND-004 sahipliğinde), tests/Host/MigrationComposition/Manifest/ManifestTests.cs
  (V1-FND-004 sahipliğinde) — migration 138 pozisyonunun kaydı
  (operations.offsite_backup_receipts), PhaseBMax 137→138 (V1-SET-008
  emsali).
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  src/Modules/Operations/packages.lock.json ve ALKAROS.Operations.csproj'a
  bağımlı ~37 test projesinin kendi packages.lock.json'ları (V1-FND-001
  sahipliğinde, FIND-IA-0043 emsali) — yeni ProjectReference'ların
  transitive kapanışı `dotnet restore --force-evaluate` ile mekanik olarak
  yeniden üretildi, elle içerik değiştirilmedi.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz —
  retroaktif olarak eklendi, 2026-09-22 bağımsız denetim: bu üç dosya
  `105f725d` commit'inde değiştirilmiş ama burada belgelenmemişti):
  docs/architecture/module-dependency-rules.md, src/Modules/Operations/
  OperationsModule.cs (V1-RMD-002 sahipliğinde), tests/Architecture/
  ModuleBoundaries/ModuleBoundaryTests.cs (V1-FND-013 sahipliğinde) —
  Operations'ın Security'ye olan gerçek derleme bağımlılığı `DependsOn`/
  `ApprovedEdges`'e eklendi (mimari testin bu bağımlılığı hiç
  denetlemediği bir kör nokta kapatıldı).

## In scope

- İstemci tarafı şifreleme, sağlama toplamı, retry, saklama, değişmez yapıt meta verileri ve alert hatası.
- Fiscal/audit verisi için RPO=0 mekanizması (WAL/continuous streaming arşivleme) ve financial veri için 15 dk akış;
  off-site yükleme günlük ritimde.

## Out of scope

- Geri yükleme orkestrasyonu ve yerel yedekleme oluşturma.

## Dependencies

- V1-OPS-002
- V0-BKP-001
- V0-BKP-002
- V15-SEC-001

## Deliverables

- `src/Modules/Operations/OffsiteBackup/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- **İndirilen yapı sağlama toplamı eşleşir ve yetkili anahtar olmadan geri yüklenemez:**
  `OffsiteBackupUploadServiceTests.UploadAsyncRoundTripsChecksumMatchesAndDecryptsWithAuthorizedKey`
  (gerçek şifrele→yükle→indir→çöz→checksum karşılaştırması) ve
  `OffsiteBackupUploadServiceTests.DecryptWithoutAuthorizedKeyVersionThrowsDecryptionFailed`
  (yetkisiz/anahtarsız bir provider ile decrypt `OffsiteBackupDecryptionFailedException`
  fırlatır). `evidence/V15-BKP-001/test-offsitebackup.txt` — 24/24 geçti.
- **Yükleme hatası görünür ve güvenli şekilde yeniden denenir:**
  `UploadAsyncTransientFailuresBelowMaxAttemptsRetriesAndSucceeds` (2 geçici
  hata sonrası 3. denemede başarı, `RetryPolicy`'nin exponential backoff'u
  ile) ve `UploadAsyncFailuresExhaustMaxAttemptsEmitsCriticalAlertAndThrows`
  (3 deneme de başarısız → `IStructuredEventLogger`'a
  `offsitebackup.upload.failed` Critical event + `OffsiteBackupUploadFailedException`).
- **Ölçülen backup sıklığı/RPO eşiği karşılaştırması:** `RpoCoverageChecker`
  + 4 test — en yeni receipt'in yaşı `docs/recovery/rpo-rto-targets.md`'nin
  onaylı hedefine (Fiscal 5dk, OrdersInventory 1sa, Settings 24sa) göre
  ölçülüyor.
- **RPO=5dk (WAL) karşılanma ölçümü:** `WalArchiveFreshnessChecker` + 5 test
  — yerel WAL arşivi ile off-site kopyanın en yeni segment farkını,
  `archive_timeout=300s`'in garantisiyle (bekleyen her segment ≥5dk) ölçer.
  **Dürüst sınır:** bu, gerçek `alkaros-wal-archive` cilt/hacmine ve gerçek
  bir off-site hedefe karşı canlı bir ölçüm DEĞİL — algoritma birim
  testleriyle doğrulandı; gerçek `alkaros-wal-archive` dizin listesini ve
  gerçek bir off-site target'ın `ListArtifactIdsAsync()`'ini bu checker'a
  bağlayan canlı entegrasyon `V15-BKP-002`/`V20-DRL-001`'in restore-drill
  kapsamına bırakıldı (bu task'ın "Out of scope"unda zaten "geri yükleme
  orkestrasyonu" olarak dışlanmıştı).
- **Migration ileri/geri:** `evidence/V15-BKP-001/migration-138-up-down.txt`
  — boş bir veritabanında gerçek `psql` ile up→down, ikisi de temiz.
- **Build/denetim:** `evidence/V15-BKP-001/build-release.txt` (0 uyarı/0
  hata), `evidence/V15-BKP-001/consistency-audit.txt` (temiz),
  `evidence/V15-BKP-001/plan-audit-validate.txt` (0 hata/0 uyarı),
  `evidence/V15-BKP-001/project-manifest.txt` (VALID).
- **Semih'in elle deneyebileceği senaryo:** `LocalDirectoryOffsiteBackupTarget`
  ile gerçek bir `backup.sh` çıktısını (`.dump` + `.sha256`) bir
  `BackupArtifactReference`'a sarıp `OffsiteBackupUploadService.UploadAsync`'e
  ver; ikinci dizine (`offsite` simülasyonu) şifreli dosyanın düştüğünü,
  aynı artifact id'yle tekrar yüklemenin reddedildiğini, ve
  `OffsiteBackupRestoreVerificationService.DownloadAndVerifyAsync`'in
  orijinal pg_dump baytlarını checksum'ı doğrulayarak geri verdiğini gör.
- **2026-09-22 sonradan düzeltme (CRITICAL)** (bağımsız denetim): production
  kodu `OffsiteBackupUploadService.cs:95`'te retry tükendiğinde
  `EventNameConvention.IsValid`'in reddettiği alt çizgili bir event adı
  (`offsite_backup.upload_failed`) yayınlıyordu; gerçek
  `StructuredEventLogger` bu adı `ArgumentException` ile reddediyor, yani
  yedekleme kalıcı olarak başarısız olduğunda operatöre gitmesi gereken
  Critical alert HİÇ YAYINLANMIYORDU. Kök neden:
  `tests/Modules/Operations/OffsiteBackup/Fixtures/RecordingStructuredEventLogger.cs`
  test double'ı `EventNameConvention.IsValid`'i hiç uygulamıyordu, bu yüzden
  mevcut test yeşil geçiyordu ama gerçek DI grafiğiyle asla çalışmazdı.
  Düzeltme: event adı `offsitebackup.upload.failed` olarak değiştirildi
  (regex'e karşı doğrulandı), test double gerçek convention'ı uygulayacak
  şekilde güncellendi (ve aynı desendeki
  `tests/Modules/Operations/RestoreVerification/Fixtures/RecordingStructuredEventLogger.cs`
  de sınırlı ek olarak aynı şekilde düzeltildi), revert-and-confirm ile
  eski adla testin gerçekten `ArgumentException` fırlattığı kanıtlandı.
  Kanıt: `evidence/V15-BKP-001/2026-09-22-event-name-fix-revert-and-confirm.txt`.

## Handoff

- V15-BKP-002

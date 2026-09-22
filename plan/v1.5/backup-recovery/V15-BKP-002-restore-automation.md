# V15-BKP-002 - Implement isolated restore verification

- Task ID: V15-BKP-002
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.38-I.44
- PDF:II.2.23
- PDF:III.25

## Goal

Isolated PostgreSQL instance'a restore işlemini otomatikleştirmek ve integrity/application smoke kontrollerini
çalıştırmak.

## Owned surface

- `src/Modules/Operations/RestoreVerification/**`, `tests/Modules/Operations/RestoreVerification/**`,
  `deployment/restore/**`, `database/migrations/V15/V15-BKP-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  ALKAROS.slnx (V1-FND-001 sahipliğinde),
  build/project-manifest.json (V1-FND-007 sahipliğinde) — yeni
  `ALKAROS.Operations.RestoreVerification.Tests` proje kaydı eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs — migration 139
  (`operations.restore_attempts`) pozisyonunun kaydı; `PhaseBMax` 138→139.

## In scope

- Yapı seçimi, şifre çözme, geri yükleme, bütünlük sorguları, uygulama başlatma dumanı ve sonuç kaydı.

## Out of scope

- Production felaket kararı ve tam kurtarma tatbikatı.

## Dependencies

- V15-BKP-001
- V0-BKP-001
- V0-BKP-002

## Deliverables

- `src/Modules/Operations/RestoreVerification/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- `RestoreVerificationOrchestrator`: `V15-BKP-001`'in `OffsiteBackupRestoreVerificationService`'i (indir+şifre
  çöz+checksum) ile başlar, gerçek bir izole PostgreSQL veritabanı (`NpgsqlIsolatedRestoreDatabaseFactory` —
  `CREATE DATABASE`/`DROP DATABASE ... WITH (FORCE)`, her denemede tek kullanımlık) sağlar, plaintext SQL script'i
  uygular, adlandırılmış bütünlük sorgularını (`IntegrityCheck`) ve bir uygulama başlatma dumanını (`SELECT 1`)
  çalıştırır, süreyi ölçer ve sonucu (`operations.restore_attempts`, migration 139) her zaman kaydeder — başarı ya
  da başarısızlık.
- Gerçek test (`RunAsyncThrowsAndRecordsFailureWhenChecksumIsTampered`,
  `RunAsyncThrowsAndRecordsFailureWhenCiphertextIsCorrupted`): bozuk (checksum uyuşmayan veya AES-GCM tag'i
  doğrulanmayan) bir yapıt, `CountingIsolatedRestoreDatabaseFactory.ProvisionCallCount == 0` ile kanıtlandığı üzere,
  izole veritabanı HİÇ sağlanmadan reddedilir — "uygulama başlatılmadan önce başarısız olur" maddesi tam bu.
  `RunAsyncThrowsAndRecordsFailureWhenIntegrityCheckFails`: SQL uygulandıktan sonra bütünlük sorgusu beklenen veriyi
  bulamazsa `RestoreIntegrityCheckFailedException` ile reddedilir.
- `RunAsyncRestoresArtifactAndRecordsSuccessWithinRto`: gerçek bir restore + bütünlük sorgusu + smoke check
  başarıyla tamamlanır, ölçülen süre `RtoTargets.For(dataClass)` (Fiscal=2sa/OrdersInventory=4sa/Settings=8sa,
  `docs/recovery/rpo-rto-targets.md` §2) ile karşılaştırılıp `WithinRtoTarget` alanına kaydedilir.
- `dotnet build ALKAROS.slnx -c Release` → 0 uyarı, 0 hata
  (`evidence/V15-BKP-002/build-release.txt`).
- `dotnet test ALKAROS.Operations.RestoreVerification.Tests` → 6/6
  (`evidence/V15-BKP-002/test-restoreverification.txt`). Regresyon:
  `Operations.OffsiteBackup` 24/24, `Architecture.Tests` (module boundary) 9/9,
  `Host.Tests` (migration Manifest) 17/17 — hepsi yeşil
  (`evidence/V15-BKP-002/test-regression.txt`).
- Migration 139 gerçek Postgres 18'e (docker `alkaros-test-pg`) karşı ayrı bir scratch veritabanında ileri/geri
  doğrulandı (`evidence/V15-BKP-002/migration-139-verify.txt`).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı.
- `python tools/consistency-audit/consistency_audit.py` → temiz.
- `python tools/project-manifest/project_manifest_tool.py` → VALID.
- **Dürüst sınır:** restore edilen "yapı" bu görevde plaintext bir SQL script olarak modellendi (Npgsql üzerinden
  doğrudan uygulanıyor), `deploy/docker/backup.sh`'ın ürettiği `pg_dump` custom-format (binary) artifact'lerle
  bire bir aynı format değil — `V15-BKP-001`'in "yerel yedekleme oluşturma" kapsam dışı kararıyla aynı gerekçe:
  hangi görev gerçek artifact'i üretirse, format uyumu o görevin/entegrasyonun konusu olur. Bu görev restore
  ORKESTRASYONUNU (seçim/çöz/uygula/doğrula/kaydet) ve gerçek bir izole Postgres'e karşı çalıştığını kanıtlıyor.

## Handoff

- V20-DRL-001

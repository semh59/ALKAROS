# V15-SEC-003 - Harden sensitive payload retention

- Task ID: V15-SEC-003
- Status: Done
- Assignee: Claude Sonnet 5
- Work type: implementation
- Surface state: Existing

## Source basis

- PDF:I.38-I.44
- PDF:II.11-II.12
- PDF:III.33-III.34

## Goal

V1-SEC-002 sınırı üzerinde retention enforcement, authorized re-encryption ve deletion scheduling uygulamak.

## Owned surface

- `src/Modules/Security/DataProtectionRetention/**`, `tests/Modules/Security/DataProtectionRetention/**`,
  `database/migrations/V15/V15-SEC-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  src/Modules/Security/ALKAROS.Security.csproj, src/Modules/Security/packages.lock.json
  (V15-SEC-001/V15-SEC-002 sahipliğinde) — yeni ALKAROS.SensitiveData ve ALKAROS.Audit proje
  referansları + Npgsql paket referansı eklendi.
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  ALKAROS.slnx (V1-FND-001 sahipliğinde), build/project-manifest.json (V1-FND-007 sahipliğinde) —
  yeni ALKAROS.Security.DataProtectionRetention.Tests proje kaydı eklendi (C86/C88/C91/V15-SEC-002 emsali).
- Sınırlı ek (paylaşılan, geri-tik olmadan, path bilerek backtick'siz):
  database/MigrationComposition/order.json, src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs (V0-DAT-001 sahipliğinde) —
  migration 137 pozisyonunun kaydı (security.retention_subjects).

## In scope

- Retention execution, authorized re-encryption, deletion queue, legal hold conflict ve coverage verification.
- Provider payload retention silme/anonimleştirme bu görevde; iş kayıtları PII silme V15-KVK-001 kapsamında; V0-CMP-003
  disposal matrisi üstündür.

## Out of scope

- Base encryption/redaction, customer anonymization workflow ve secret rotation.

## Dependencies

- V0-CMP-003
- V1-OPS-001
- V15-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/Security/DataProtectionRetention/**` altında production code ve task-specific automated test assets.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Expired payload, V0-CMP-003 disposal matrisine göre Anonymize edilir; yalnız matriste Delete sınıfındaki veriler
  silinir; re-encryption ve deletion retry idempotent'tır; plaintext/log leakage yoktur.
- `security.retention_subjects` (migration 137) — 9 `DataCategory` (V0-CMP-003'ün KVKK envanterinden birebir),
  `DisposalMatrix` her kategoriyi Anonymize/Delete/Retain'e dispatch eder; `RetentionCoverageVerifier` her ikisinin
  (Actions + RetentionPeriods) tüm enum değerlerini kapsadığını doğrular.
- `RetentionExecutionService.RunSweepAsync`: süresi dolmuş `ProviderPayloads` gerçekten Anonymize edilir (envelope
  gerçek olmayan bir sentinel key-id ile üzerine yazılır, `SensitivePayloadProtector.Unprotect` artık başarısız
  olur); legal-hold ve Retain-sınıfı asla dispose edilmez; henüz süresi dolmamış konu dokunulmaz; sweep'i iki kez
  çalıştırmak idempotent (ikinci koşuda hiçbir şey yeniden işlenmez/çift sayılmaz).
- `DeletionQueueProcessor`: Delete-sınıfı bir konu önce kuyruğa alınır (envelope hâlâ yerinde), yalnız processor
  gerçekten satırı kalıcı siler; boş kuyrukta veya ikinci çalıştırmada no-op (hata değil).
- `AuthorizedReEncryptionService`: eski anahtardan yeni anahtara gerçek round-trip (plaintext korunur, retention
  saati -- tablo'nun kendi `created_at` kolonu -- değişmez); zaten hedef anahtardaki bir konu no-op; disposed bir
  konu veya yanlış eski anahtar fail-closed.
- Her disposal/purge/re-encryption, `V1-OPS-001`'in `IAuditEventStore`'u üzerinden kaydedilir (yalnız
  kategori/aksiyon/aktör metadata'sı — envelope veya plaintext asla değil).
- `dotnet build ALKAROS.slnx -c Release` → 0 uyarı, 0 hata (`evidence/V15-SEC-003/build-release.txt`).
- `ALKAROS.Security.DataProtectionRetention.Tests`: 32/32
  (`evidence/V15-SEC-003/test-dataprotectionretention.txt`).
- Regresyon: `ALKAROS.Security.SecretRotation.Tests` 28/28, `ALKAROS.Security.IdentityHardening.Tests` 12/12,
  `ALKAROS.Audit.EventStore.Tests` 22/22, migration `ManifestTests` 16/16 — hepsi yeşil
  (`evidence/V15-SEC-003/test-regression-*.txt`).
- Migration 137 gerçek PostgreSQL 18'e karşı ileri/geri doğrulandı (`evidence/V15-SEC-003/migration-137-up-down.txt`).
- `python tools/plan-audit/plan_audit_tool.py validate` → 0 hata, 0 uyarı
  (`evidence/V15-SEC-003/plan-audit-validate.txt`).
- `python tools/consistency-audit/consistency_audit.py` → temiz (`evidence/V15-SEC-003/consistency-audit.txt`).
- `python tools/project-manifest/project_manifest_tool.py` → VALID
  (`evidence/V15-SEC-003/project-manifest-validate.txt`).
- **2026-09-22 sonradan düzeltme** (bağımsız denetim): migration 137'de 3 gerçek kusur (kullanılmayan partial index —
  predicate gerçek sorguyla eşleşmiyordu; idempotent olmayan `CREATE TABLE`/`CREATE INDEX`; eksik `data_category`
  CHECK constraint'i) ve `PostgresRetentionSubjectStore.ReadRecord`'da bir kod-seviyesi fail-safe eksikliği
  (`Enum.Parse` → `Enum.TryParse` + yeni `RetentionSubjectCorruptDataException`) düzeltildi; ayrıca
  `GetPendingAsync`/`GetDeletionQueueAsync`'e parametrik `LIMIT` (varsayılan 1000), `DeletionQueueProcessor`'da
  audit-event/purge sıralaması (artık purge başarıyla bittikten SONRA audit yazılıyor) ve
  `RetentionExecutionService.RunSweepAsync`'te `Retain` aksiyon kontrolünün süre kontrolünden önce yapılması
  (FiscalData/InvoiceData artık doğru şekilde `SkippedRetain`'e düşüyor) düzeltildi. 40/40 test (8 yeni), migration
  ileri/geri gerçek Postgres'e karşı yeniden doğrulandı — bkz. `evidence/V15-SEC-003/README.md`'nin "2026-09-22
  sonradan düzeltilen kusurlar" bölümü ve `evidence/V15-SEC-003/2026-09-22-audit-fix-*.txt`.
- **2026-09-23 not** (bağımsız denetim): `SecurityModule.cs`'e kayıtla
  `RetentionExecutionService`/`DeletionQueueProcessor`/
  `AuthorizedReEncryptionService` artık gerçekten DI'dan çözülebiliyor
  (bkz. `71f28599`; `AuthorizedReEncryptionService`'in kendi bare-string
  `accessor` parametresi kaldırılıp yeni `RetentionAccessPolicy` ile
  DI-uyumlu hale getirildi). Ama hiçbiri henüz bir HTTP endpoint'e veya
  zamanlanmış bir arka plan işine (scheduled job) bağlı değil — retention
  sweep/deletion-queue/re-encryption'ı bugün hiçbir şey otomatik veya
  yönetici-tetiklemeli olarak çalıştıramaz; bu, ayrı bir Host-wiring/
  scheduling görevi gerektiriyor (bu task'ın Owned surface'ı hiçbir zaman
  Host/HTTP dosyalarını kapsamadı).

## Handoff

- V15-KVK-001
- V15-KVK-002
- V20-SEC-001

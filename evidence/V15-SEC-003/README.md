# V15-SEC-003 - Kanıt özeti

Sensitive-payload retention: `security.retention_subjects` (migration 137),
V0-CMP-003's disposal matrix (9 kategori) dispatch edilerek retention
execution / re-encryption / deletion queue / legal hold / coverage
verification uygulandı.

## Dosyalar

- `build-release.txt` — `dotnet build -c Release`, 0 hata/0 uyarı.
- `test-dataprotectionretention.txt` — yeni test projesi, 32/32.
- `test-regression-secretrotation.txt` — V15-SEC-001 regresyon, 28/28.
- `test-regression-identityhardening.txt` — V15-SEC-002 regresyon, 12/12.
- `test-regression-manifesttests.txt` — migration manifest testleri, 16/16.
- `migration-137-up-down.txt` — gerçek PostgreSQL 18'e karşı ileri/geri.
- `plan-audit-validate.txt`, `consistency-audit.txt`,
  `project-manifest-validate.txt` — hepsi temiz.

## 2026-09-22 sonradan düzeltilen kusurlar (bağımsız denetim)

Bağımsız bir denetim, `Done` durumundaki bu task'ın migration'ında ve store'unda
5 gerçek kusur buldu; hepsi aynı oturumda düzeltildi (henüz hiçbir "canlı"
ortamda uygulanmamış migration olduğu için 138 numaralı yeni bir migration
gerekmedi, dosyanın kendisi düzeltildi):

1. `ix_retention_subjects_pending` predicate'i (`... AND legal_hold = FALSE`)
   gerçek sorguyla (`WHERE disposed_at IS NULL`) eşleşmiyordu, planlayıcı
   index'i hiç kullanamıyordu — predicate `WHERE disposed_at IS NULL` olarak
   daraltıldı. Bkz. `2026-09-22-audit-fix-explain.txt` (20.000 satır, 400
   pending üzerinde gerçek EXPLAIN: artık `Bitmap Index Scan on
   ix_retention_subjects_pending`).
2. Migration idempotent değildi (`CREATE TABLE`/`CREATE INDEX` → `IF NOT
   EXISTS`, 138/139 ile tutarlı hale getirildi).
3. `data_category TEXT` kolonunda CHECK constraint yoktu — `DataCategory`
   enum'ındaki 9 değeri kapsayan bir CHECK eklendi.
4. `PostgresRetentionSubjectStore.ReadRecord`'daki `Enum.Parse` çağrıları
   (`data_category`, `disposal_action`) try/catch'siz idi — bozuk bir satır
   tüm batch okumasını patlatabiliyordu. `Enum.TryParse` + yeni
   `RetentionSubjectCorruptDataException` ile değiştirildi (kod-seviyesi
   fail-safe, CHECK constraint'ten bağımsız).
5. `GetPendingAsync`/`GetDeletionQueueAsync` LIMIT'siz sorgulardı (büyüyen
   tabloda sessiz yük riski) — `PostgresOffsiteBackupReceiptStore` deseniyle
   tutarlı, varsayılanı 1000 olan parametrik `limit` eklendi.

Aynı oturumda, aynı dizindeki (`src/Modules/Security/DataProtectionRetention/**`)
iki ayrı düşük-öncelikli bulgu da düzeltildi:

6. `DeletionQueueProcessor.ProcessAsync`: audit event artık `PurgeAsync`
   BAŞARIYLA tamamlandıktan SONRA yazılıyor (önceden tersiydi — purge
   başarısız olursa audit trail'de gerçekleşmemiş bir purge için sahte kayıt
   riski vardı).
7. `RetentionExecutionService.RunSweepAsync`: `Retain` aksiyon kontrolü artık
   süre kontrolünden ÖNCE yapılıyor — `FiscalData`/`InvoiceData` (her ikisi de
   `Retain`, süre `null`) artık doğru şekilde `SkippedRetain` kovasına
   düşüyor (davranış zaten doğruydu, yalnız raporlama kovası yanlıştı).

Yeni testler: `2026-09-22-audit-fix-tests.txt` (40/40, eski 32 + 8 yeni:
index-usage EXPLAIN testi, CHECK constraint reddi, `ReadRecord` corrupt-data
exception'ı, LIMIT davranışı x2, purge-önce-audit sıralaması, SkippedRetain
kovası x2). Migration ileri/geri: `2026-09-22-audit-fix-migration-up-down.txt`.
Build: `2026-09-22-audit-fix-build.txt` (0/0). Manifest regresyon:
`2026-09-22-audit-fix-manifest-tests.txt` (17/17).

## Bilinen sınır (Handoff, bu görevin kapsamı dışında)

`tools/consistency-audit/consistency_audit.py`'nin `MODULE_SCHEMA` sözlüğü
`Security` modülünü içermiyor — bu, V11-RMD-002'nin kapattığı 5-modül kör
noktasıyla aynı sınıf bir boşluk (yeni bir modül eklendiğinde ayrı bir
remediation task'ı bunu MODULE_SCHEMA'ya eklemek zorunda). Bu task'ın kendi
Owned surface'ı `tools/consistency-audit/**`'i kapsamıyor, bu yüzden
düzeltilmedi — yalnız not düşüldü.

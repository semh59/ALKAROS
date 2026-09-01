# V1-RMD-086 - PostgreSQL backup restore mechanism and runbook

- Task ID: V1-RMD-086
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Planned

## Source basis

- PO:2026-09-01
- PDF:II.2.23
- PDF:III.25
- EXT:POSTGRESQL-18.4

## Goal

ALKAROS dağıtımının PostgreSQL veritabanı için operatörün elle çalıştırabileceği bir yedekleme ve geri yükleme mekanizması yoktur; `V1-OPS-002` yalnızca soyut bir bayt yazma motoru sağlar, gerçek `pg_dump`/`pg_restore` yolu, sağlama toplamı doğrulaması, bozuk yedek reddi, ölçülmüş süre ve runbook eksiktir. Bu görev `pg_dump` özel biçim yedeği, SHA-256 sağlama toplamı yan dosyası, sağlama toplamı zorunlu geri yükleme, Compose `ops` profili, atılabilir PostgreSQL 18 üzerinde uçtan uca kendi kendini doğrulayan round-trip betiği ve operatör runbook'unu ekler.

## Owned surface

- `plan/v1/remediation/V1-RMD-086-postgresql-backup-restore-mechanism-and-runbook.md`
- `deploy/docker/backup.sh`, `deploy/docker/restore.sh` ve `deploy/docker/backup-restore-selfcheck.sh` betikleri.
- `docs/recovery/**` altındaki operatör runbook'u ve ölçülen RPO/RTO girdileri.
- `compose.yaml` içindeki `backup` servisi (yalnızca `ops` profili).
- `evidence/V1-RMD-086/**` ve `evidence/v0/recovery/V0-BKP-001/**` altındaki round-trip kanıt paketleri.

## In scope

- `pg_dump --format=custom` ile zaman damgalı yedek üreten ve yanına `.sha256` yan dosyası yazan `deploy/docker/backup.sh`.
- Geri yüklemeden önce SHA-256 sağlama toplamını doğrulayan, uyuşmazlıkta geri yüklemeyi reddeden ve sıfırdan temiz hedef veritabanına `pg_restore` uygulayıp süreyi ölçen `deploy/docker/restore.sh`.
- `postgres:18` imajını kullanan, normal `up` sırasında çalışmayan, yalnızca `docker compose --profile ops run --rm backup` ile tetiklenen Compose servisi.
- Kendi atılabilir kaynak ve hedef veritabanını oluşturan, doğrulama tablosunu tohumlayan, yedek alan, bir kopyayı bozup geri yüklemenin reddedildiğini doğrulayan, temiz yedeği geri yükleyip satır ve veri sağlama toplamının eşleştiğini kanıtlayan ve süreleri raporlayan `deploy/docker/backup-restore-selfcheck.sh`.
- `docs/recovery/backup-restore-runbook.md` operatör yordamı: yedek alma, saklama önerisi, geri yükleme adımları, bozuk yedek davranışı, ölçülen yedek/geri yükleme süreleri ve `V0-BKP-001` ile ilişkinin açıklanması.

## Out of scope

- Tesis dışı şifreli yedek ve zamanlanmış otomatik geri yükleme; `V15-BKP-001` ve `V15-BKP-002` kapsamındadır.
- `src/Modules/Operations/BackupHealth/**` üretim kodunu değiştirmek; sağlık modülü sözleşmesi `V1-OPS-002` sahipliğindedir.
- `V0-BKP-002` sayısal RPO/RTO hedeflerini onaylamak; bu adlandırılmış işletme onayı ve ayrı bir decision görevi gerektirir.

## Dependencies

- V1-GOV-048

## Deliverables

- `deploy/docker/backup.sh`, `deploy/docker/restore.sh`, `deploy/docker/backup-restore-selfcheck.sh` betikleri ve `compose.yaml` `ops` profili `backup` servisi.
- `docs/recovery/backup-restore-runbook.md` operatör runbook'u.
- `evidence/V1-RMD-086/roundtrip.md` ve `evidence/v0/recovery/V0-BKP-001/` altında tarihli round-trip transcript'i.

## Acceptance evidence

- Atılabilir PostgreSQL 18 üzerinde çalıştırılan `backup-restore-selfcheck.sh` çıktısı (exit 0): tohumlanan 500 satırlık tablo temiz hedefe geri yüklendiğinde satır sayısı (500 == 500) ve veri md5'i (`3300c8dd...` == `3300c8dd...`) eşleşir. Kanıt: `evidence/V1-RMD-086/roundtrip.md`.
- Kasıtlı olarak bozulan yedek artefaktı `restore.sh` tarafından çıkış kodu 4 ile reddedilir ve hedef veritabanı hiç oluşturulmaz.
- Gerçek ALKAROS şeması (57 tablo) `alkaros-postgres-1` üzerinde yedeklenip `alkaros_restore` içine geri yüklenir; `pg_restore --exit-on-error` sıfır hata, tablo pariteği 57 == 57. Kanıt: `evidence/V1-RMD-086/real-schema-check.log`.
- `docker compose --profile ops run --rm backup` ve aynı servisle `restore.sh` çağrısı uçtan uca çalışır; artefakt ve `.sha256` yan dosyası `alkaros-backups` volume'una yazılır, geri yükleme sağlama toplamı doğrulamasından geçer (her ikisi exit 0).
- Transcript'e `pg_dump`/`pg_restore` komutları, artefakt SHA-256 hash'i, çıkış kodları ve ölçülen süreler yazılır.

## Handoff

- V1-GOV-049

# V1-RMD-095 - WAL archiving and point-in-time recovery

- Task ID: V1-RMD-095
- Status: Done
- Assignee: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Work type: implementation
- Surface state: Existing

## Source basis

- PO:2026-09-01
- PDF:II.2.23
- PDF:III.25
- EXT:POSTGRESQL-18.4

## Goal

13. dalga yalnız mantıksal `pg_dump` yedeği sağladı; bu, geri yükleme noktası olarak WAL oynatmayı desteklemez ve RPO'yu yedek aralığıyla (saatlik) sınırlar. Bu görev PostgreSQL WAL arşivlemesini ve point-in-time recovery yolunu ekler: `postgresql.tuned.conf` içinde `archive_mode = on`, `archive_command` mount'lu `/wal-archive` hacmine kopyalar, `archive_timeout = 300` ile en fazla 5 dakikada bir segment döner. `deploy/docker/basebackup.sh` `pg_basebackup` ile sıkıştırılmış fiziksel base backup + SHA-256 yan dosyası üretir; `deploy/docker/restore-pitr.sh` base backup'ı sağlama toplamıyla doğrular, ayrı bir veri dizinine açar, `pg_control`'den primary parametrelerini eşler, `restore_command` + `recovery_target_time` yazar ve hedef zamana kadar WAL oynatır; canlı kümeye hiç dokunmaz. `deploy/docker/pitr-selfcheck.sh` atılabilir PostgreSQL 18 üzerinde uçtan uca kanıt üretir. Rakip araştırmasına göre kalibre edilmiş RPO/RTO hedef tablosu `docs/recovery/rpo-rto-targets.md` içine `V0-BKP-002`'den devralınarak yazılır ve Semih (product owner) tarafından onaylanır.

## Owned surface

- `plan/v1/remediation/V1-RMD-095-wal-archiving-point-in-time-recovery.md`
- `deploy/docker/postgresql.tuned.conf`
- `deploy/docker/pg_hba.conf`
- `deploy/docker/basebackup.sh`
- `deploy/docker/restore-pitr.sh`
- `deploy/docker/pitr-selfcheck.sh`
- `docs/recovery/rpo-rto-targets.md`
- `evidence/V1-RMD-095/**`

## In scope

- `postgresql.tuned.conf`: `wal_level = replica`, `archive_mode = on`, `archive_command`, `archive_timeout = 300`, `hba_file`.
- `pg_hba.conf`: açık host tabanlı kimlik doğrulama; `samenet` replication satırları `pg_basebackup` için, her non-local bağlantı `scram-sha-256` parolası ister.
- `compose.yaml`: `alkaros-wal-archive` adlı hacim ve postgres'e mount; root entrypoint sarmalayıcısı hacmi postgres kullanıcısına verir; `ops` profilinde `basebackup` servisi.
- `basebackup.sh`: `pg_basebackup --format=tar --gzip`, SHA-256 yan dosyası, çıktıyı okunur yapmak.
- `restore-pitr.sh`: sağlama toplamı kapısı (exit 4), boş olmayan hedef reddi (exit 2), eksik base backup (exit 3), `pg_control` parametre eşleme, `recovery_target_time`/`latest`, yalnız yan veri dizinine yazma.
- `pitr-selfcheck.sh`: base backup + "korunacak" satırlar + hedef zaman + "kaybolacak" satırlar → geri yükleme → hedef zamana kadar satırlar var, sonrasındakiler yok.
- `docs/recovery/rpo-rto-targets.md`: rakip taahhüt tablosu, kalibre RPO/RTO tablosu, Semih onay bloğu, ne dokunulmaz notu, WAL/PITR operatör yordamı ve WAL arşivi budama/tesis dışı taşıma önerisi.

## Out of scope

- Streaming replikasyon / hot standby (`V15-BKP`).
- Şifreli tesis dışı arşiv taşıması ve arşiv budama otomasyonu; runbook yordam olarak belgeler.
- Uygulama kodu; hiç değişmez.

## Dependencies

- V1-GOV-066

## Deliverables

- WAL arşivleme yapılandırması ve `basebackup.sh` / `restore-pitr.sh` / `pitr-selfcheck.sh` betikleri.
- Kalibre ve onaylı `docs/recovery/rpo-rto-targets.md`.
- `evidence/V1-RMD-095/` altında self-check çıktısı ve canlı yığına karşı uçtan uca PITR transkripti.

## Acceptance evidence

- `deploy/docker/pitr-selfcheck.sh` atılabilir PostgreSQL 18'de exit 0: hedef zamana kadar 150 satır kurtarılır, sonrasındaki 50 satır düşürülür.
- Canlı compose yığınına karşı: `basebackup` servisi fiziksel base backup üretir; `restore-pitr.sh` hedef zamana oynatır; kurtarılan kümede hedef öncesi satırlar var, sonrası yok.
- `pg_stat_archiver.failed_count = 0` ve WAL segmentleri `alkaros-wal-archive` hacminde birikir.
- `docs/recovery/rpo-rto-targets.md` rakip tablosu, kalibre hedefler ve Semih onay bloğu içerir.

## Handoff

- V1-GOV-067

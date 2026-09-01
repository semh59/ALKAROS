# V0-BKP-001 - Validate PostgreSQL backup and restore tooling

- Task ID: V0-BKP-001
- Status: Done
- Assignee: Semih (product owner)
- Work type: validation
- Surface state: Existing

## Source basis

- PDF:II.2.23
- PDF:III.25
- EXT:POSTGRESQL-18.4

## Goal

Disposable PostgreSQL 18 instance üzerinde backup, checksum ve restore tool path uygulanabilirliğini doğrulamak.

## Owned surface

- `evidence/v0/recovery/V0-BKP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Seeded verification table, backup artifact, checksum, corruption rejection, clean restore ve ölçülen süre.

## Out of scope

- ALKAROS production schema, application startup, scheduling, retention ve off-site automation.

## Dependencies

- V0-DAT-001

## Onay

- Onaylayan: Semih — Founder / Product Owner
- Onay tarihi: 2026-09-01
- Karar: Windows ikinci-instance shared-memory error 487 bloğu, ayrı ve stabil bir Docker `postgres:18` konteyneri sağlanınca çözüldü. Tam backup/restore transkripti bu ortamda üretildi: `V1-RMD-086` self-check (500 satır seed → backup → checksum → bozuk artefakt exit 4 ile reddedilir → temiz restore, satır sayısı ve veri md5 eşleşir, ölçülen süre) ve gerçek ALKAROS şeması (57 tablo / 14 şema / 69 FK / 38 migration) backup → `alkaros_restore` restore → nesne pariteği; 2026-09-01 tarihli yeniden doğrulama `evidence/V1-GOV-065/restore-drill-2026-09-01.md`. `V1-RMD-095` bunun üstüne fiziksel base backup + WAL point-in-time recovery ekledi ve `evidence/V1-RMD-095/**` altında uçtan uca PITR transkripti üretti.
- Kanıt yüzeyi: `evidence/v0/recovery/V0-BKP-001/**`, `evidence/V1-RMD-086/**`, `evidence/V1-GOV-065/restore-drill-2026-09-01.md`, `evidence/V1-RMD-095/**`.

## Deliverables

- V0-BKP-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Temiz PostgreSQL 18 instance'a restore edilen seeded kayıt ve checksum eşleşir; corrupted artifact application kanıtı
  sayılmadan reddedilir.
- checksum hash: SHA-256; restore komutu exit code 0; evidence'e komut transcript'i, artifact hash'i ve ölçülen süre
  yazılır.

## Handoff

- V1-OPS-002
- V15-BKP-001
- V15-BKP-002

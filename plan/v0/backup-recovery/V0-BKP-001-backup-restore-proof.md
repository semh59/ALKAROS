# V0-BKP-001 - Validate PostgreSQL backup and restore tooling

- Task ID: V0-BKP-001
- Status: Blocked
- Assignee: codex-v0-bkp-001
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

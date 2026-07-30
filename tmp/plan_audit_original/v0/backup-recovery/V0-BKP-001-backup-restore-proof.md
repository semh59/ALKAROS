# V0-BKP-001 - Validate backup and restore path

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Yerel ve off-site backup hedefinden boş bir makineye geri dönüşün uygulanabilirliğini kanıtlamak.

## Owned surface

- `evidence/v0/recovery/V0-BKP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Artifact encryption, checksum, key availability, PostgreSQL restore ve application startup verification.

## Out of scope

- Production scheduling ve retention automation.

## Dependencies

- V0-DAT-001

## Deliverables

- V0-BKP-001 için tarihli ve kaynakları belirtilmiş evidence package.
- Başarı ve en az bir gerçek hata/edge-case çıktısı.
- Doğrulanamayan maddeler için açık blocker kaydı; varsayımla kapatma yok.

## Acceptance evidence

- Bağımsız test dizininde restore edilen veritabanı açılıyor ve kritik doğrulama sorguları beklenen sonucu veriyor.

## Handoff

- V1-OPS-002, V15-BKP-001 ve V15-BKP-002.


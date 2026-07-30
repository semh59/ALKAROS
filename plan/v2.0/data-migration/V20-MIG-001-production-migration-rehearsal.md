# V20-MIG-001 - Rehearse production migration

- Task ID: V20-MIG-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation
- Surface state: Planned

## Source basis

- PDF:I.45-I.54
- PDF:II.13-II.15
- PDF:III.29-III.40
- CORR:C1

## Goal

Representative sanitized dataset üzerinde production migration path'in tamamını çalıştırmak ve integrity, duration ve
resource usage değerlerini ölçmek.

## Owned surface

- `release/evidence/migration/forward/**`, `tools/release/migration-rehearsal/**`
- Bu görev ürün migration dosyalarını değiştiremez; hata ilgili migration sahibine geri döner.

## In scope

- Ön kontrol kontrolleri, yedekleme checkpoint, sıralı migration, bütünlük sorguları, mutabakat toplamları ve zamanlama.

## Out of scope

- Şemanın yeniden tasarlanması, veri temizleme politikası ve production yürütme.

## Dependencies

- V0-DAT-001
- V15-BKP-002
- V15-REC-001
- V20-INS-001
- V20-INS-002
- V1-FND-004
- V0-DAT-006

## Deliverables

- Tekrarlanabilir prova prosedürü ve imzalı sonuç kaydı.
- Before/after row, money, stock ve Invoice control total değerleri.

## Acceptance evidence

- release adayı, onaylanan pencere içinde geçiş yapar ve onaylanan her bütünlük/kontrol-toplam sorgusu, temsili veri
  kümesine aktarılır.

## Handoff

- V20-MIG-002

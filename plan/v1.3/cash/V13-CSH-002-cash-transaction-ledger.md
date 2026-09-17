# V13-CSH-002 - Implement CashTransaction ledger and close difference

- Task ID: V13-CSH-002
- Status: Done
- Assignee: claude-code-session_01Xsqh6z1RYhmFapKkHKoBmk
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.38-I.44
- PDF:II.2.7
- PDF:II.5.9
- PDF:III.9

## Goal

Cash sale/refund/in/out entry'lerini kaydetmek ve expected/actual close variance değerini hesaplamak.

## Owned surface

- `src/Modules/Cash/TransactionLedger/**`, `tests/Modules/Cash/TransactionLedger/**`,
  `database/migrations/V13/V13-CSH-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.
- Kapsam genişletme onayı (2026-09-17 kullanıcı talimatı): bu task'ın yeni
  test projesinin `ALKAROS.slnx` ve `build/project-manifest.json` içine
  kaydı (V11-UNT-001/V13-CSH-001/V13-ALC-001 emsaliyle aynı desen).
- Sınırlı ek (paylaşılan, geri-tik olmadan): database/MigrationComposition/order.json,
  src/Host/Composition/Migrations/MigrationManifest.cs,
  tests/Host/MigrationComposition/Manifest/ManifestTests.cs — yeni migration
  pozisyonunun kaydı (V13-ALC-001 emsaliyle aynı desen).

## In scope

- Pozitif büyüklük/yön kuralları, payment bağlantısı, açık düzeltme ve yakın projeksiyon.

## Out of scope

- Banka/yemek kartı işlemleri ve genel muhasebe muhasebesi.

## Dependencies

- V13-CSH-001
- V13-PAY-001
- V0-DAT-004

## Deliverables

- `src/Modules/Cash/TransactionLedger/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Oturumda cash'nin değişmez girişlerden yeniden oluşturulması bekleniyor; fark kaydedilir, asla sessizce üzerine
  yazılmaz.

## Handoff

- V13-CSH-003
- V13-REC-001
- V14-ACC-005

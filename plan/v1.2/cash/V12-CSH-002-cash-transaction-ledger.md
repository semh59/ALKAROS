# V12-CSH-002 - Implement CashTransaction ledger and close difference

- Task ID: V12-CSH-002
- Status: Planned
- Assignee: Unassigned (exactly one person)
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
  `database/migrations/V12/V12-CSH-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Pozitif büyüklük/yön kuralları, payment bağlantısı, açık düzeltme ve yakın projeksiyon.

## Out of scope

- Banka/yemek kartı işlemleri ve genel muhasebe muhasebesi.

## Dependencies

- V12-CSH-001
- V12-PAY-001
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

- V12-CSH-003
- V12-REC-001
- V13-ACC-005

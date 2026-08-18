# V1-TBL-003 - Implement reversible table merge records

- Task ID: V1-TBL-003
- Status: Done
- Assignee: Antigravity-v1-tbl-003
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.7-I.10
- PDF:II.2.3
- PDF:II.3.16
- PDF:II.5.15
- PDF:III.5

## Goal

Source Table veya Order silmeden multi-table merge membership ve explicit undo modelini uygulamak.

## Owned surface

- `src/Modules/Tables/TableMerge/**`, `tests/Modules/Tables/TableMerge/**`,
  `database/migrations/V1/V1-TBL-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Unpaid Bill'ler için merge group, participant, primary table, history ve undo command.

## Out of scope

- Fiziksel oturma rezervasyonu, şubeler arası table hareketi ve payment verisi bulunan merge; sonuncunun sahibi
  `V12-TBL-001`dir.

## Dependencies

- V1-TBL-001
- V1-ORD-001
- V1-BIL-001
- V0-DOM-002
- V1-FND-005
- V1-OPS-001

## Deliverables

- `src/Modules/Tables/TableMerge/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Üç-table birleştirme temsil edilebilir; tersine çevirme, yalnızca eşzamanlılık ön koşulları sağlandığında ilişkileri
  geri yükler; hiçbir geçmiş silinmez.
- Payment verisi bulunan participant bu V1 komutuyla birleştirilemez.

## Handoff

- V1-TBL-005
- V12-TBL-001

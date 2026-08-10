# V12-REC-001 - Implement payment fiscal cash and meal-card reconciliation

- Task ID: V12-REC-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.21
- PDF:II.3.15
- PDF:II.5.12
- PDF:II.6.11
- PDF:III.23

## Goal

V1.2 yetkili kaynakları farklılaştığında tekilleştirilmiş ReconciliationCase kayıtları oluşturun.

## Owned surface

- `src/Modules/Reconciliation/Payments/**`, `tests/Modules/Reconciliation/Payments/**`,
  `database/migrations/V12/V12-REC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Hugin Unknown, approved-without-allocation, allocation/provider mismatch, fiscal mismatch, cash farkı ve meal-card
  settlement mismatch kaynak çiftleri.
- Terminal totals mismatch (kaynak: V12-HUG-004) kaynak çifti.

## Out of scope

- QNB, çevrimiçi provider ve birleşik kontrol paneli.

## Dependencies

- V12-HUG-002
- V12-HUG-003
- V12-FSC-002
- V12-PAY-004
- V12-ALC-004
- V12-CSH-002
- V12-MCD-002
- V12-MCD-004
- V1-REC-001

## Deliverables

- `src/Modules/Reconciliation/Payments/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Aynı çözülmemiş uyumsuzluk, her iki tarafın da tanımlandığı açık bir vakaya yol açar; çözüm yalnızca ekleme amaçlıdır
  ve denetlenir.
- `V12-MCD-004` tarihli `NotApplicable` ise meal-card divergence kaynağı disabled olarak kaydedilir; Hugin, fiscal ve
  cash reconciliation kaynakları yine doğrulanır.
- Aynı durumda `V12-MCD-002` de aynı dated decision ile `NotApplicable` olabilir; task kalan reconciliation
  kaynaklarıyla çalışmaya devam eder.
- `V12-FSC-002` tarihli `NotApplicable` ise meal-card fiscal closure branch'i bu reconciliation'da disabled kalır;
  fiscal ve cash kaynak çiftleri yine doğrulanır.
- Terminal totals sapmaları ReconciliationCase kaydını yalnız `V12-REC-001` API'si üzerinden üretir; `V12-HUG-004`
  doğrudan case yazamaz.

## Handoff

- V15-REC-001
- V15-REC-002

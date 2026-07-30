# V12-RPT-001 - Implement payment cash fiscal and meal-card reports

- Task ID: V12-RPT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.20
- PDF:II.10
- PDF:III.31

## Goal

payment karışımı, cash oturumu, mali status ve yemek kartı kapatma raporlarını uygulayın.

## Owned surface

- `src/Modules/Reporting/Payments/**`, `tests/Modules/Reporting/Payments/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- İş tarihi/terminal/provider filtreleri, net geri ödeme tutarları, cash farkı ve mutabakat toplamları.

## Out of scope

- CustomerAccount, invoice ve çevrimiçi kanal raporları.

## Dependencies

- V0-DOM-008
- V12-ALC-003
- V12-ALC-004
- V12-CSH-002
- V12-MCD-002
- V12-FSC-001

## Deliverables

- `src/Modules/Reporting/Payments/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Rapor toplamları, tam/kısmi geri ödemelerin ardından yetkili defterlerle mutabakata varır;
  Bilinmeyen/ReconciliationRequired ayrı ayrı görülebilir.
- `V12-MCD-002` tarihli `NotApplicable` ise meal-card settlement bölümü disabled olarak işaretlenir; payment ve cash
  raporları aynı acceptance kanıtlarıyla çalışmaya devam eder.

## Handoff

- V15-RPT-001

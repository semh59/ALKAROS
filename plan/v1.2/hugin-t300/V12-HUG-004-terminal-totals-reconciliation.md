# V12-HUG-004 - Implement T300 terminal totals reconciliation

- Task ID: V12-HUG-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:II.2.16
- PDF:II.3.12
- PDF:II.5.4
- PDF:III.19

## Goal

Yerel onaylı/iade edilmiş kart işlemlerini terminalin doğrulanmış toplamları veya işlem sorgu kaynağıyla karşılaştırın.

## Owned surface

- `src/Modules/Reconciliation/HuginTotals/**`, `tests/Modules/Reconciliation/HuginTotals/**`,
  `database/migrations/V12/V12-HUG-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Dönem/kesim kimliği, terminal referans eşleştirmesi, eksik/ekstra işlem ve sapma tespiti; case üretimi
  `V12-REC-001` API'si üzerinden.

## Out of scope

- Doğrulanmış T300 contract dışındaki banka ödemesi.

## Dependencies

- V12-HUG-001
- V12-HUG-003
- V12-REC-001
- V0-HUG-001
- V1-SEC-001
- V1-SEC-002

## Deliverables

- `src/Modules/Reconciliation/HuginTotals/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bilinen test periyodu sıfır farkla uzlaşır; enjekte edilen eksik/ekstra işlem, izlenebilir bir vaka oluşturur.
- `V12-REC-001` tarihli `NotApplicable` ise payment reconciliation case üretimi bu task kapsamında doğrulanmaz; Hugin
  terminal totals karşılaştırması kendi doğrulanmış toplam/işlem sorgu kaynaklarıyla yine doğrulanır.

## Handoff

- V12-REC-001
- V15-REC-001

# V1-BIL-001 - Implement Bill foundation and source links

- Task ID: V1-BIL-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.11-I.15
- PDF:II.2.5
- PDF:II.3.3
- PDF:II.5.2
- PDF:III.7

## Goal

Bill, BillItem ve V0-DOM-002 tarafından seçilen referentially safe Order/OrderItem source ilişkisini uygulamak.

## Owned surface

- `src/Modules/Billing/BillFoundation/**`, `tests/Modules/Billing/BillFoundation/**`,
  `database/migrations/V1/V1-BIL-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Bill oluşturma, kaynak bağlantıları, parasal anlık görüntüler ve payment olmayan status alt kümesi.

## Out of scope

- Payment tahsisi, ücretli kapatma ve geri ödeme.

## Dependencies

- V1-ORD-001
- V0-DOM-002
- V0-CMP-002

## Deliverables

- `src/Modules/Billing/BillFoundation/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bir Bill, birden fazla Siparişi kaynaklayabilir ve bir Order, kopyalanan miktar veya tutar olmadan Faturalar arasında
  bölünebilir.

## Handoff

- V1-BIL-002
- V1-TBL-002
- V12-ALC-002

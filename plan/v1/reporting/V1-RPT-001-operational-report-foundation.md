# V1-RPT-001 - Implement V1 operational reports

- Task ID: V1-RPT-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.20
- PDF:II.10
- PDF:III.31

## Goal

Onaylanmış ölçüm sözleşmelerini kullanarak order, table, garson ve yazdırma hatası raporlarını uygulayın.

## Owned surface

- `src/Modules/Reporting/V1Operations/**`, `tests/Modules/Reporting/V1Operations/**`,
  `database/migrations/V1/V1-RPT-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- İş tarihi filtreleri, order/table/waiter granularity, print status ve reconciliation toplamları.

## Out of scope

- Payment, envanter, invoice ve çevrimiçi kanal ölçümleri.

## Dependencies

- V0-DOM-008
- V1-ORD-001
- V1-TBL-001
- V1-KIT-003

## Deliverables

- `src/Modules/Reporting/V1Operations/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Rapor toplamları, başlangıç ​​senaryoları için kaynak sorgularıyla mutabakata varır; saat dilimi/hizmet günü sınırı
  testleri başarılı oldu.

## Handoff

- V15-RPT-001

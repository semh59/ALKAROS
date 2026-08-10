# V1-TBL-001 - Implement Table lifecycle and persistence

- Task ID: V1-TBL-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.7-I.10
- PDF:II.2.3
- PDF:II.3.16
- PDF:II.5.15
- PDF:III.5

## Goal

Table identity, zone, canonical status transition ve optimistic concurrency davranışını uygulamak.

## Owned surface

- `src/Modules/TableManagement/TableLifecycle/**`, `tests/Modules/TableManagement/TableLifecycle/**`,
  `database/migrations/V1/V1-TBL-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Table/bölge kısıtlamaları, null yapılabilir bölge benzersizlik politikası, durum komutları ve satır sürümü
  kontrolleri.

## Out of scope

- Table aktarımı, birleştirme, rezervasyon rezervasyonu ve bill kapatma bağlantısı.

## Dependencies

- V1-FND-001
- V0-DOM-001
- V0-DAT-003

## Deliverables

- `src/Modules/TableManagement/TableLifecycle/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Eşzamanlı eski geçiş reddedilir; kanonik geçişler geçer; yinelenen table sayı politikası NULL anlambilimiyle
  uygulanır.

## Handoff

- V1-TBL-002
- V1-TBL-003
- V1-ORD-001

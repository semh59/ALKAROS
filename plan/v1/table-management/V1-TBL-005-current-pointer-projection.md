# V1-TBL-005 - Implement Table current pointer projection

- Task ID: V1-TBL-005
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.3
- PDF:II.3.16
- PDF:II.5.15
- PDF:III.5

## Goal

Authoritative source ilişkilerinden current Order/Bill pointer projection'larını üretmek ve rebuild etmek.

## Owned surface

- `src/Modules/Tables/CurrentPointers/**`, `tests/Modules/Tables/CurrentPointers/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Atomik güncellemeler, eski işaretçi tespiti, rebuild ve çoklu açık kaynak politikası.

## Out of scope

- Table aktarma/birleştirme komutları ve Order oluşturma.

## Dependencies

- V1-TBL-001
- V1-TBL-002
- V1-TBL-003
- V1-ORD-001
- V1-BIL-001
- V0-DAT-004

## Deliverables

- `src/Modules/Tables/CurrentPointers/**` altında Goal kapsamını uygulayan production code ve task-specific
  automated test assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Projeksiyon rebuild canlı değerlere eşittir; eski önbellek hiçbir zaman yetkili Order/Bill sahipliğini değiştirmez.
- Order creation, table transfer ve table merge sonrasında pointer update aynı transaction sonucu ile eşleşir; her
  failure/retry ve full rebuild aynı authoritative owner ilişkisini üretir.

## Handoff

- V1-CUI-001
- V15-REC-001

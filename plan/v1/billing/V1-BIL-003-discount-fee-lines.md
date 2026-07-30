# V1-BIL-003 - Implement approved discount and fee bill lines

- Task ID: V1-BIL-003
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.5
- PDF:II.3.3
- PDF:II.5.2
- PDF:III.7

## Goal

Yalnız onaylanmış discount, fee ve tip line type'larını tax ve authorization kurallarıyla hesaplamak ve kalıcılaştırmak.

## Owned surface

- `src/Modules/Billing/Adjustments/**`, `tests/Modules/Billing/Adjustments/**`, `database/migrations/V1/V1-BIL-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Hat sahipliği, sebep, onay, vergi tahsisi ve deterministik yuvarlama.

## Out of scope

- Kampanya motoru, maaş bordrosu ödemesi ve provider promosyonları.

## Dependencies

- V1-BIL-001
- V0-DOM-006
- V0-CMP-002
- V0-CMP-004

## Deliverables

- `src/Modules/Billing/Adjustments/**` altında Goal kapsamını uygulayan production code ve task-specific automated test
  assets.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Desteklenmeyen ücret türleri mümkün değildir; izin verilen ayarlama toplamları satır ile bill arasında mutabakat
  sağlar ve tamamen denetlenir.

## Handoff

- V12-ALC-002

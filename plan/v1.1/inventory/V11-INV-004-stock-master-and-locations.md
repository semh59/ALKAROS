# V11-INV-004 - Implement StockItem and StockLocation master data

- Task ID: V11-INV-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:II.2.12
- PDF:II.3.9
- PDF:II.5.6
- PDF:II.5.14
- PDF:III.14

## Goal

Stok kimliklerini, stok türlerini, takip edilen birim ve konum yapılandırmasını uygulayın.

## Owned surface

- `src/Modules/Inventory/StockMaster/**`, `tests/Modules/Inventory/StockMaster/**`,
  `database/migrations/V11/V11-INV-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Üründen stoka eşleme kardinalite, RawMaterial/Portion/Paketleme/ServiceItem, temel birim ve aktif konum.

## Out of scope

- Hareketler, dengeler ve satın alma UI.

## Dependencies

- V1-CAT-001
- V11-UNT-001
- V0-DAT-003

## Deliverables

- `src/Modules/Inventory/StockMaster/**` altında Goal kapsamını uygulayan production code ve task-specific automated
  test assets.
- Public contract ve otomatik başarı/ret/concurrency testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Her hareket hedefinin geçerli bir izlenen birimi vardır; yinelenen/boş konumlu anahtar politikası uygulanır; etkin
  olmayan öğe yeni hareketi reddeder.

## Handoff

- V11-INV-001
- V11-INV-002
- V11-PUR-001

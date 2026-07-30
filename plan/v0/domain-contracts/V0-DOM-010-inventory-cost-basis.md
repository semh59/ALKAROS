# V0-DOM-010 - Define inventory cost basis

- Task ID: V0-DOM-010
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision
- Surface state: Planned

## Source basis

- PDF:I.22.1
- PDF:II.2.10
- PDF:III.12
- CORR:C9
- CORR:C12

## Goal

Reçete/üretim quantity hesap sırasını, historical cost kaynağını ve stok değerleme yöntemini tek
inventory calculation basis olarak belirlemek.

## Owned surface

- `docs/domain/inventory-cost-basis.md`
- Bu görev başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Waste factor ile unit conversion uygulama sırası, cost event zamanı, valuation method, rounding, negative stock ve
  correction davranışı.

## Out of scope

- Accounting general ledger, production code ve report UI.

## Dependencies

- V0-CMP-002
- V0-DAT-002

## Deliverables

- Tek decision record: kaynaklar, erişim tarihleri, onaylayan, seçilen sonuç, reddedilen alternatifler ve etkilenen task
  kimlikleri.
- Seçilen quantity-order ve cost formula için örnek hesaplar.

## Acceptance evidence

- Aynı recipe, waste factor, unit conversion, stock history ve business date girdisi tek tekrarlanabilir base-unit
  consumption ve historical cost sonucu üretir.

## Handoff

- V11-RCP-002
- V11-PRD-001
- V11-PRD-002
- V11-RPT-001

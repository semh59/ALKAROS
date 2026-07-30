# V0-DOM-003 - Define full and partial refund ledger

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Goal

Tam ve kısmi iadelerin Payment, PaymentAllocation, Bill ve FiscalDocument üzerindeki etkisini immutable ledger olarak tanımlamak.

## Owned surface

- `docs/domain/refund-ledger.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Partial refund allocation, cumulative limits, reversal references, bill reopening ve fiscal linkage.

## Out of scope

- Hugin protokol çağrıları ve refund UI.

## Dependencies

- V0-DOM-002

## Deliverables

- V0-DOM-003 için bağlayıcı karar veya contract dokümanı.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- 100 ödeme / 20 iade örneği 80 net paid amount verir; double refund ve over-refund yasakları açık.

## Handoff

- V12-ALC-003 ve V12-HUG-003.


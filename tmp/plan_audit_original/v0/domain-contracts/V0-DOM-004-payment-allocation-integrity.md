# V0-DOM-004 - Define PaymentAllocation integrity rules

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Goal

Payment, Bill, BillAllocation ve PaymentAllocation arasındaki çapraz kimlik ve para bütünlüğünü tek invariant setinde kapatmak.

## Owned surface

- `docs/domain/payment-allocation-integrity.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Same-bill checks, currency equality, remaining amount, validity, idempotency ve compensating records.

## Out of scope

- Tender-specific provider davranışları.

## Dependencies

- V0-DOM-002,V0-DOM-003

## Deliverables

- V0-DOM-004 için bağlayıcı karar veya contract dokümanı.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Yanlış Bill'e allocation, fazla allocation, farklı currency ve duplicate replay için açık ret kuralları var.

## Handoff

- V12-ALC-001 ve V12-ALC-002.


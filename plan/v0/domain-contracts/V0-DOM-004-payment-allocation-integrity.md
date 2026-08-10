# V0-DOM-004 - Define PaymentAllocation integrity rules

- Task ID: V0-DOM-004
- Status: Done
- Assignee: codex-v0-dom-004
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:I.11-I.15
- PDF:II.2.6
- PDF:II.3.4-II.3.5
- PDF:II.5.3
- PDF:III.8
- CORR:C4

## Goal

Payment, Bill, BillAllocation ve PaymentAllocation arasındaki çapraz kimlik ve para bütünlüğünü tek invariant setinde
kapatmak.

## Owned surface

- `docs/domain/payment-allocation-integrity.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Same-bill checks, currency equality, remaining amount, validity, idempotency ve compensating records.

## Out of scope

- Tender-specific provider davranışları.

## Dependencies

- V0-DOM-002
- V0-DOM-003

## Deliverables

- V0-DOM-004 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- Yanlış Bill'e allocation, fazla allocation, farklı currency ve duplicate replay için açık ret kuralları var.

## Handoff

- V12-ALC-001
- V12-ALC-002

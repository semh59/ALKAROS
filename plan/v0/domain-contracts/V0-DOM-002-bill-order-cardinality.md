# V0-DOM-002 - Close Bill to Order cardinality

- Task ID: V0-DOM-002
- Status: Blocked
- Assignee: codex-v0-dom-002
- Work type: decision
- Surface state: Existing

## Source basis

- PDF:I.11-I.15
- PDF:II.2.5
- PDF:II.3.3
- PDF:II.5.2
- PDF:III.7

## Goal

Bir Bill'in bir veya daha fazla Order/OrderItem kaynağını ve bir Order'ın birden fazla Bill'e bölünmesini temsil eden
tek ilişki modelini seçmek.

## Owned surface

- `docs/domain/bill-order-cardinality.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Junction entity, identity, uniqueness, split ve table-merge etkileri.

## Out of scope

- Payment allocation, refund veya UI akışları.

## Dependencies

- None

## Blocker

- Mevcut decision record N:1 Bill-to-Order modelini kabul ederken aynı Bill için tek Order invariantı koymaktadır. Ancak
  örnek veriyle tutarlı cardinality, split ve merge kararını named approver onaylayınca görev yeniden `Planned`
yapılabilir.

## Deliverables

- V0-DOM-002 için tek decision record: kaynak + erişim tarihi + onaylayan + seçilen sonuç + reddedilen alternatifler +
  etkilenen task kimlikleri.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- 1:N, N:1 ve split senaryoları örnek veriyle kayıpsız temsil ediliyor; tek `bills.order_id` bağımlılığı kaldırılacak
  biçimde karar verilmiş.

## Handoff

- V1-BIL-001
- V1-BIL-002

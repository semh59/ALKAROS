# V0-DOM-002 - Close Bill to Order cardinality

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: decision

## Goal

Bir Bill'in bir veya daha fazla Order/OrderItem kaynağını ve bir Order'ın birden fazla Bill'e bölünmesini temsil eden tek ilişki modelini seçmek.

## Owned surface

- `docs/domain/bill-order-cardinality.md`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Junction entity, identity, uniqueness, split ve table-merge etkileri.

## Out of scope

- Payment allocation, refund veya UI akışları.

## Dependencies

- None

## Deliverables

- V0-DOM-002 için bağlayıcı karar veya contract dokümanı.
- En az iki pozitif ve iki negatif örnek.
- Tüketici görevler için açık input/output ve invariant listesi.

## Acceptance evidence

- 1:N, N:1 ve split senaryoları örnek veriyle kayıpsız temsil ediliyor; tek `bills.order_id` bağımlılığı kaldırılacak biçimde karar verilmiş.

## Handoff

- V1-BIL-001 ve V1-BIL-002.

